# pbi-rest-proxy

This project is being rebuilt from scratch.

The new target is a standalone Windows app that:

- authenticates the current user
- discovers accessible Power BI / Fabric semantic models
- connects directly to a selected semantic model over XMLA
- exposes the selected model locally through a REST API
- includes a small built-in UI for connection management, DAX testing, and logs

Current implemented milestone:

- desktop WinForms shell with `Connection`, `Data Source`, `DAX`, and `Log` tabs
- in-memory session and status infrastructure
- Azure CLI-assisted or manual access-token loading with local JWT claim parsing
- Power BI workspace and semantic model discovery from the `Data Source` tab
- selected-model connect/disconnect flow with computed XMLA workspace endpoint
- localhost-only REST server with `GET /health`, `GET /info`, and `POST /execute-dax`
- DAX execution against the connected semantic model over XMLA, with compact JSON rows plus column metadata
- default DAX safeguards: `30` second command timeout and `1000` row limit

Skipped for now:

- metadata exploration over XMLA, because the current target permission model does not allow internal semantic-model metadata discovery

## Prerequisites

- Windows x64
- .NET 8 SDK

The current project targets `net8.0-windows` and uses WinForms, so it should be built and run on Windows.

### Install .NET 8 SDK

Using WinGet from PowerShell or Command Prompt:

```powershell
winget install Microsoft.DotNet.SDK.8
```

Then verify the installation:

```powershell
dotnet --info
```

You do not need to install a separate runtime if you already installed the SDK.

### Optional: Visual Studio

If you want to work in Visual Studio instead of the command line, install Visual Studio 2022 version 17.8 or later with the `.NET desktop development` workload.

## Build

From PowerShell or Command Prompt in the repository root:

```powershell
dotnet restore .\pbi-rest-proxy.sln
dotnet build .\pbi-rest-proxy.sln -c Debug
```

For a Release build:

```powershell
dotnet build .\pbi-rest-proxy.sln -c Release
```

For a timestamped publish build and packaged zip artifact, use the root build script:

```cmd
build.cmd
```

The script publishes for `win-x64`, stamps the build metadata, writes the published files under `artifacts\publish\<timestamp>\`, and creates a zip artifact under `artifacts\`.

## Run

Run the desktop app from the project file:

```powershell
dotnet run --project .\src\pbi-rest-proxy.csproj -c Debug
```

Or start it from Visual Studio by opening `pbi-rest-proxy.sln` and running the `pbi-rest-proxy` project.

## Current token test flow

1. Start the desktop app.
2. If Azure CLI is not installed yet, install it:

```powershell
winget install --exact --id Microsoft.AzureCLI
```

3. In the app, click `Login + Get Token via Azure CLI`.

Alternative manual shell flow:

```powershell
az login --allow-no-subscriptions
az account get-access-token --resource https://analysis.windows.net/powerbi/api --query accessToken -o tsv
```

If you need a specific tenant:

```powershell
az login --tenant <tenant-id-or-domain> --allow-no-subscriptions
```

4. If you used the manual shell flow instead of the app button, copy the returned token and paste it into the `Connection` tab.
5. Confirm that the app shows the parsed user, tenant, audience, and expiration details.
6. Open the `Data Source` tab and click `Load Workspaces`.
7. Select a workspace and click `Load Models`.
8. Select a semantic model and click `Connect`.
9. Confirm that `Current Selection` shows the connected target, XMLA endpoint, and local REST server URL.
10. Open the `DAX` tab and run `EVALUATE ROW("Status", "Ready")`.
11. Confirm that a pretty-printed JSON result is shown.
12. Call the local REST server:

```powershell
Invoke-RestMethod http://127.0.0.1:51087/health
Invoke-RestMethod http://127.0.0.1:51087/info
```

`POST /execute-dax` examples:

PowerShell:

```powershell
Invoke-RestMethod http://127.0.0.1:51087/execute-dax -Method Post -ContentType "application/json" -Body '{"query":"EVALUATE ROW(\"Status\", \"REST\")"}'
```

`curl` from PowerShell:

```powershell
curl --% http://127.0.0.1:51087/execute-dax -H "Content-Type: application/json" --raw-data '{"query":"EVALUATE ROW(\"Status\", \"REST\")"}'
```

The `--%` is important when you run `curl` from PowerShell, otherwise PowerShell rewrites the argument quoting before `curl` receives it.

13. Check the `Log` tab for auth, discovery, connection, REST, and DAX events.

`POST /execute-dax` returns:

- compact row arrays in `rows`
- explicit column metadata in `columns`
- exact column names as returned by ADOMD, including empty or duplicate captions
- target metadata such as `workspace`, `semanticModel`, and `xmlaEndpoint`
- `isTruncated` and `rowLimit` when the result is capped by the configured row limit

Current DAX execution defaults:

- command timeout: `30` seconds
- row limit: `1000`

Behavior when a limit is hit:

- row limit: the request still succeeds with `200 OK`, but the payload reports `isTruncated: true`
- timeout: the REST API returns `504 Gateway Timeout`, and the DAX tab reports a timeout instead of generic failure

The working plan lives in [TODO.md](TODO.md).
