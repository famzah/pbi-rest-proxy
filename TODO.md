# TODO

## Product direction

Transform `pbi-rest-proxy` into a standalone Windows app that:

- authenticates the current user against Microsoft Entra
- discovers accessible Power BI / Fabric semantic models
- lets the user select one semantic model and connect to it directly via XMLA (`powerbi://...`)
- exposes the selected model locally through a REST API
- includes a small built-in UI for connection management, ad-hoc DAX execution, and logs

## Authentication options

### Option A: Dedicated Entra app registration + MSAL public client

Description:
- Register a dedicated single-tenant public client app for `pbi-rest-proxy`
- Use MSAL for silent-first token acquisition with interactive fallback
- Use WAM/system browser for login, MFA, and conditional access

Pros:
- Cleanest long-term architecture
- Fully owned app identity
- Best auditability and least surprise
- No dependency on external CLI tools at runtime

Cons:
- Requires Entra app registration approval in the tenant
- More implementation work up front

Status:
- Architecturally preferred long-term option
- Currently blocked by tenant permissions

### Option B: Reuse a Microsoft-owned app ID such as Power BI Desktop

Description:
- Attempt to acquire tokens using a first-party Microsoft app registration

Pros:
- No need to request a new app registration

Cons:
- Unsupported and brittle
- We do not control redirects, permissions, or future changes
- Incorrect audit/sign-in identity
- Not a reliable product foundation

Status:
- Rejected

### Option C: Manual token injection

Description:
- User acquires a bearer token externally
- User pastes the token into the app
- App keeps the token in memory only and uses it for REST discovery and XMLA/ADOMD access

Pros:
- Smallest implementation surface
- No Entra app registration needed for the app
- Works with the existing user identity and MFA handled externally

Cons:
- Manual and awkward UX
- Token refresh is manual
- Bearer token handling requires care

Status:
- Acceptable fallback
- Keep as a supported fallback even if other auth flows are added later

### Option D: Azure CLI-assisted auth

Description:
- Use Azure CLI as the interactive login mechanism
- App shells out to:
  - `az login --allow-no-subscriptions`
  - `az account get-access-token --resource https://analysis.windows.net/powerbi/api`
- App consumes the returned token

Pros:
- Supported tooling
- Uses standard Microsoft browser login and MFA flow
- Proven workable in the current environment
- Smaller scope than implementing MSAL from scratch

Cons:
- Requires Azure CLI to be installed
- Still depends on an external tool
- Less polished than in-app auth

Status:
- Recommended MVP auth path

### Option E: Azure PowerShell-assisted auth

Description:
- Use Azure PowerShell for login and token acquisition
- App shells out to:
  - `Connect-AzAccount`
  - `Get-AzAccessToken -ResourceUrl "https://analysis.windows.net/powerbi/api"`

Pros:
- Supported tooling
- Similar benefits to Azure CLI

Cons:
- Slightly clumsier for token extraction in automation
- Less convenient than Azure CLI for MVP integration

Status:
- Valid fallback to Azure CLI

### Option F: Azure.Identity (`AzureCliCredential` / `AzurePowerShellCredential`)

Description:
- Use Azure.Identity inside the app to reuse an already signed-in Azure CLI or Azure PowerShell session

Pros:
- Cleaner code-level integration than manual shell parsing
- Can reduce explicit token copy/paste UX

Cons:
- Still depends on CLI/PowerShell sign-in existing outside the app
- Slightly more abstraction/risk for MVP than directly shelling out to proven commands

Status:
- Candidate follow-up after MVP

### Option G: Service principal / daemon auth

Description:
- Authenticate as an app/service identity instead of the signed-in user

Pros:
- Good for unattended jobs

Cons:
- Not aligned with the local single-user interactive tool use case
- Different permissions and governance model
- Does not satisfy the current user/MFA requirement

Status:
- Out of scope for the current direction

## Chosen MVP direction

Build a standalone Windows app with:

- `WinForms` UI
- Azure CLI-assisted auth as the primary path
- manual token paste as a fallback
- local REST API retained
- direct DAX test UI retained
- local in-memory session for the selected semantic model

## Current progress

Implemented:

- Phase 1 desktop WinForms shell with `Connection`, `Data Source`, `DAX`, and `Log` tabs
- `Connection` now focuses on session status and access token management
- `Data Source` now focuses on current selection and discovery
- Phase 2 in-memory session and status infrastructure
- Phase 3 Azure CLI-assisted and manual token loading
- Phase 4 Power BI workspace and semantic model discovery
- Phase 5 connected target selection with computed XMLA endpoint
- Phase 7 DAX execution against the connected semantic model

Skipped:

- Phase 6 XMLA metadata exploration for tables, columns, and relationships
  because the current access model only guarantees DAX/query access, while XMLA metadata discovery requires higher semantic-model permissions that are not available in the target environment

## MVP scope

### UI tabs

#### Connection tab

Purpose:
- manage auth and session state

Contents:
- current auth state
- token source (`Azure CLI` or `Manual`)
- signed-in user display
- tenant ID or tenant hint if available
- token expiration timestamp
- actions:
  - `Login via Azure CLI`
  - `Refresh token`
  - `Paste token`
  - `Clear token`
- manual token input

#### Data Source tab

Purpose:
- manage workspace/model discovery and current selection

Contents:
- current connection summary:
  - selected workspace
  - selected semantic model
  - connection state
  - connected workspace
  - connected semantic model
  - XMLA server
  - local REST endpoint
- `Load workspaces/models`
- `Connect`
- `Disconnect`
- workspace list
- semantic model list

#### DAX tab

Purpose:
- execute ad-hoc DAX without going through the REST client path

Contents:
- multiline DAX editor
- `Execute` button
- result grid
- execution summary:
  - row count
  - elapsed time
  - current target model

#### Log tab

Purpose:
- expose operational visibility inside the app

Contents:
- timestamped log list
- auth events
- discovery/connect events
- DAX execution events
- REST access log entries similar to a web-server access log
- error entries with human-readable detail

### Backend capabilities

#### Local REST API

Keep:
- `GET /health`
- `GET /info`
- `POST /execute-dax`

Change:
- `/info` should return the currently selected cloud model instead of startup CLI values
- request logging should flow to the in-app log sink as well as normal logging infrastructure

#### Semantic model discovery

Use standard Microsoft APIs only.

Initial target:
- list accessible workspaces
- list semantic models inside accessible workspaces

Nice to have later:
- owner display enrichment
- last refresh display
- endorsement/sensitivity display

#### XMLA execution

Use:
- `powerbi://...` workspace endpoint
- semantic model name as catalog/database
- `AdomdConnection.AccessToken`

#### Session model

App session should keep:
- current access token in memory only
- current signed-in user metadata extracted from the JWT
- selected workspace
- selected semantic model
- connection state

Do not do in MVP:
- persist token to disk
- log token contents
- auto-refresh tokens without re-acquiring them

## Implementation plan

### Phase 1: Restructure the app for a desktop host

- create the new solution/project/app under the `pbi-rest-proxy` name from the start
- introduce a WinForms entry point
- host the ASP.NET Core local REST server in-process
- keep the app structure cleanly separated into UI, session, discovery, query, and REST layers

### Phase 2: Add session and status infrastructure

- introduce an app session service for:
  - access token
  - token metadata
  - selected workspace/model
  - connection status
- introduce a UI-safe log sink that both the REST server and app services can write to

### Phase 3: Add token acquisition

Primary path:
- shell out to Azure CLI for login and token retrieval

Fallback path:
- manual token paste

Implementation notes:
- accept raw JWT or `Bearer <token>`
- decode JWT locally to display:
  - expiration
  - tenant
  - user/account hints
- validate that the token is not obviously malformed
- treat the token as a secret

### Phase 4: Add discovery services

- call Microsoft Power BI / Fabric APIs with the current token
- load workspaces
- load semantic models for the selected workspace
- map them into simple UI rows

### Phase 5: Add connect/select behavior

- when the user clicks `Connect`, set the selected workspace/model in session
- compute the XMLA endpoint
- update status UI
- enable DAX and REST execution against the selected model

### Phase 6: Add metadata exploration

Status:
- Skipped

Notes:
- full XMLA metadata discovery requires higher semantic-model permissions than the target environment provides
- keep the phase skipped unless the permission model changes or a clearly-supported read-only metadata path is identified

### Phase 7: Add DAX execution

- execute ad-hoc DAX against the currently selected model
- show results in a grid
- surface connection and execution errors clearly

### Phase 8: Reintroduce the local REST layer

- implement:
  - `GET /health`
  - `GET /info`
  - `POST /execute-dax`

### Phase 9: Add the log tab

- show:
  - local REST access log entries
  - auth events
  - connect/disconnect events
  - DAX execution events
  - errors

### Phase 10: Polish and guardrails

- disable actions when no token or no model is selected
- show token expiration warnings
- return clear API errors when no current model is connected
- keep localhost-only binding

## Later follow-ups

- Rewrite `README.md` once the standalone app structure and usage flow are implemented
- Add a proper build script for the standalone app
- Add configurable ADOMD command timeout
- Add better cancellation handling for long-running queries
- Add CSV export endpoint for table-shaped results
- Add request metrics and optional per-request query logging
- Consider replacing shell-out Azure CLI auth with Azure.Identity or MSAL later
