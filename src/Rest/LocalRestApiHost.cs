using System.Data;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PbiRestProxy.Dax;
using PbiRestProxy.Logging;
using PbiRestProxy.Session;

namespace PbiRestProxy.Rest;

public sealed class LocalRestApiHost : IAsyncDisposable
{
    public const string DefaultBaseUrl = "http://127.0.0.1:51087";

    private readonly LogStore logStore;
    private readonly AppSessionService sessionService;
    private readonly AdomdDaxQueryService daxQueryService;
    private readonly object syncRoot = new();
    private WebApplication? webApplication;
    private LocalRestApiState state;

    public LocalRestApiHost(LogStore logStore, AppSessionService sessionService, AdomdDaxQueryService daxQueryService)
    {
        this.logStore = logStore;
        this.sessionService = sessionService;
        this.daxQueryService = daxQueryService;
        state = LocalRestApiState.Stopped(DefaultBaseUrl);
    }

    public event Action? StateChanged;

    public LocalRestApiState State
    {
        get
        {
            lock (syncRoot)
            {
                return state;
            }
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (syncRoot)
        {
            if (webApplication is not null || state.IsRunning)
            {
                return;
            }
        }

        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(LocalRestApiHost).Assembly.FullName,
                ContentRootPath = AppContext.BaseDirectory
            });

            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls(DefaultBaseUrl);
            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.WriteIndented = true;
                options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            });

            var app = builder.Build();
            ConfigurePipeline(app);

            await app.StartAsync(cancellationToken).ConfigureAwait(false);

            lock (syncRoot)
            {
                webApplication = app;
                state = LocalRestApiState.Running(DefaultBaseUrl);
            }

            logStore.WriteInfo("REST", $"Started local REST server at {DefaultBaseUrl}.");
            StateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            lock (syncRoot)
            {
                state = LocalRestApiState.Failed(DefaultBaseUrl, ex.Message);
            }

            logStore.WriteError("REST", $"Failed to start local REST server at {DefaultBaseUrl}. {ex.Message}");
            StateChanged?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        WebApplication? app;

        lock (syncRoot)
        {
            app = webApplication;
            webApplication = null;
            state = LocalRestApiState.Stopped(DefaultBaseUrl);
        }

        if (app is not null)
        {
            await app.StopAsync().ConfigureAwait(false);
            await app.DisposeAsync().ConfigureAwait(false);
            logStore.WriteInfo("REST", "Stopped local REST server.");
        }

        StateChanged?.Invoke();
    }

    private void ConfigurePipeline(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await next().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsJsonAsync(new { error = ex.Message }).ConfigureAwait(false);
                logStore.WriteError("REST", $"Unhandled error for {context.Request.Method} {context.Request.Path}. {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                logStore.WriteInfo(
                    "REST",
                    $"{context.Request.Method} {context.Request.Path}{context.Request.QueryString} -> {context.Response.StatusCode} in {stopwatch.Elapsed.TotalMilliseconds:N0} ms");
            }
        });

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            appName = "pbi-rest-proxy",
            version = PbiRestProxy.BuildInfo.Version,
            buildTimestampUtc = PbiRestProxy.BuildInfo.BuildTimestampUtc,
            restBaseUrl = DefaultBaseUrl
        }));

        app.MapGet("/info", () =>
        {
            var sessionState = sessionService.State;
            var restState = State;

            return Results.Ok(new
            {
                appName = "pbi-rest-proxy",
                version = PbiRestProxy.BuildInfo.Version,
                buildTimestampUtc = PbiRestProxy.BuildInfo.BuildTimestampUtc,
                buildTimestampLocal = PbiRestProxy.BuildInfo.BuildTimestampLocal,
                artifactSuffix = PbiRestProxy.BuildInfo.ArtifactSuffix,
                restServer = new
                {
                    isRunning = restState.IsRunning,
                    baseUrl = restState.BaseUrl,
                    status = restState.StatusText,
                    startupError = restState.StartupError
                },
                session = new
                {
                    tokenLoaded = sessionState.HasAccessToken,
                    tokenExpired = sessionState.AccessToken?.IsExpired,
                    tokenSource = sessionState.TokenSource?.ToString(),
                    signedInUser = sessionState.AccessToken?.DisplayUser,
                    tenantId = sessionState.AccessToken?.TenantId,
                    selectedWorkspace = sessionState.SelectedWorkspaceName,
                    selectedSemanticModel = sessionState.SelectedSemanticModelName,
                    connectedWorkspace = sessionState.ConnectedWorkspaceName,
                    connectedSemanticModel = sessionState.ConnectedSemanticModelName,
                    xmlaEndpoint = sessionState.XmlaEndpoint,
                    hasConnectedTarget = sessionState.HasConnectedTarget
                }
            });
        });

        app.MapPost("/execute-dax", async (ExecuteDaxRequest? request) =>
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Query))
            {
                return Results.BadRequest(new { error = "The request body must contain a non-empty 'query' value." });
            }

            if (!sessionService.TryGetConnectedQueryContext(out var queryContext, out var failureMessage))
            {
                return Results.Conflict(new { error = failureMessage });
            }

            var result = await Task.Run(
                () => daxQueryService.Execute(
                    queryContext!.AccessToken,
                    queryContext.ParsedAccessToken,
                    queryContext.XmlaEndpoint,
                    queryContext.SemanticModelName,
                    request.Query)).ConfigureAwait(false);

            return Results.Ok(CreateExecuteDaxResponse(queryContext!, result));
        });
    }

    private static ExecuteDaxResponse CreateExecuteDaxResponse(ConnectedQueryContext queryContext, DaxQueryResult result)
    {
        var columns = result.Table.Columns
            .Cast<DataColumn>()
            .Select(column => column.ColumnName)
            .ToArray();

        var rows = result.Table.Rows
            .Cast<DataRow>()
            .Select(row => row.ItemArray.Select(NormalizeValue).ToArray() as IReadOnlyList<object?>)
            .ToArray();

        return new ExecuteDaxResponse(
            queryContext.SemanticModelName,
            queryContext.WorkspaceName,
            result.Elapsed.TotalMilliseconds,
            result.RowCount,
            columns,
            rows);
    }

    private static object? NormalizeValue(object? value)
    {
        return value is DBNull ? null : value;
    }
}
