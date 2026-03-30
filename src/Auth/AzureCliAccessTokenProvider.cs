using System.Diagnostics;
using PbiRestProxy.Logging;

namespace PbiRestProxy.Auth;

public sealed class AzureCliAccessTokenProvider
{
    private const string PowerBiResource = "https://analysis.windows.net/powerbi/api";
    private readonly LogStore logStore;

    public AzureCliAccessTokenProvider(LogStore logStore)
    {
        this.logStore = logStore;
    }

    public async Task<string> AcquireAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        logStore.WriteInfo("Auth", "Starting Azure CLI token acquisition.");
        logStore.WriteInfo("Auth", "Trying Azure CLI token acquisition from the existing signed-in session first.");

        var tokenResult = await RequestAccessTokenAsync(cancellationToken);

        if (tokenResult.ExitCode == 0)
        {
            logStore.WriteInfo("Auth", "Azure CLI token acquisition succeeded without requiring a new login.");
            return ExtractAccessToken(tokenResult);
        }

        logStore.WriteWarning(
            "Auth",
            $"Azure CLI token acquisition failed on the first attempt with exit code {tokenResult.ExitCode}. {FormatFailureDetail(tokenResult)}");
        logStore.WriteInfo("Auth", "Starting fallback Azure CLI login using 'az login --allow-no-subscriptions'.");

        var loginResult = await RunShellCommandAsync(
            "az login --allow-no-subscriptions",
            cancellationToken);

        if (loginResult.ExitCode != 0)
        {
            logStore.WriteError(
                "Auth",
                $"Azure CLI login failed with exit code {loginResult.ExitCode}. {FormatFailureDetail(loginResult)}");
            throw CreateFailure("Azure CLI login failed.", loginResult);
        }

        logStore.WriteInfo("Auth", "Azure CLI login completed successfully.");
        logStore.WriteInfo("Auth", "Retrying Azure CLI token acquisition after login.");

        tokenResult = await RequestAccessTokenAsync(cancellationToken);

        if (tokenResult.ExitCode != 0)
        {
            logStore.WriteError(
                "Auth",
                $"Azure CLI token acquisition failed after login with exit code {tokenResult.ExitCode}. {FormatFailureDetail(tokenResult)}");
            throw CreateFailure("Azure CLI token acquisition failed after login.", tokenResult);
        }

        logStore.WriteInfo("Auth", "Azure CLI token acquisition succeeded after login.");
        return ExtractAccessToken(tokenResult);
    }

    private static Exception CreateFailure(string message, ShellCommandResult commandResult)
    {
        var details = FirstNonEmpty(commandResult.StandardError, commandResult.StandardOutput);

        return string.IsNullOrWhiteSpace(details)
            ? new InvalidOperationException(message)
            : new InvalidOperationException($"{message} {details.Trim()}");
    }

    private static async Task<ShellCommandResult> RunShellCommandAsync(string commandText, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /c {commandText}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ShellCommandResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static Task<ShellCommandResult> RequestAccessTokenAsync(CancellationToken cancellationToken)
    {
        return RunShellCommandAsync(
            $"az account get-access-token --resource {PowerBiResource} --query accessToken -o tsv",
            cancellationToken);
    }

    private string ExtractAccessToken(ShellCommandResult commandResult)
    {
        var accessToken = commandResult.StandardOutput.Trim();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            logStore.WriteError("Auth", "Azure CLI returned an empty access token.");
            throw new InvalidOperationException("Azure CLI returned an empty access token.");
        }

        logStore.WriteInfo("Auth", "Azure CLI returned a Power BI / Fabric access token.");
        return accessToken;
    }

    private static string FormatFailureDetail(ShellCommandResult commandResult)
    {
        var details = FirstNonEmpty(commandResult.StandardError, commandResult.StandardOutput);

        return string.IsNullOrWhiteSpace(details)
            ? "No additional Azure CLI output was captured."
            : $"Azure CLI output: {details.Trim()}";
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private sealed record ShellCommandResult(int ExitCode, string StandardOutput, string StandardError);
}
