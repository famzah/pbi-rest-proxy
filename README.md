# pbi-rest-proxy

This project is being rebuilt from scratch.

The new target is a standalone Windows app that:

- authenticates the current user
- discovers accessible Power BI / Fabric semantic models
- connects directly to a selected semantic model over XMLA
- exposes the selected model locally through a REST API
- includes a small built-in UI for connection management, DAX testing, and logs

Current implemented milestone:

- desktop WinForms shell with `Connection`, `DAX`, and `Log` tabs
- in-memory session and status infrastructure
- Azure CLI-assisted or manual access-token loading with local JWT claim parsing

Not implemented yet:

- Power BI / Fabric model discovery
- XMLA connectivity
- DAX execution
- local REST API

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
6. Check the `Log` tab for session events.

The working plan lives in [TODO.md](TODO.md).
