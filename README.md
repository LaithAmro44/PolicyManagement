# Policy Management

A small multi-tenant policy system built with .NET 10, Blazor Server, ASP.NET Core, EF Core, SQL Server, WebSockets, and WPF.

I intentionally kept the solution small. The Blazor UI, two API endpoints, and WebSocket endpoint run in one ASP.NET Core host. This makes policy changes easy to broadcast in-process and avoids adding a message broker or another internal API for a take-home assignment.

## Projects

- `PolicyManagement.Data` — entities, `PolicyDbContext`, tenant filter, seed data, and migrations.
- `PolicyManagement.Contracts` — API DTO and WebSocket message contracts shared with WPF.
- `PolicyManagement.Admin` — Blazor CRUD UI, REST API, Swagger, and WebSocket endpoint.
- `PolicyManagement.Desktop` — WPF API window and live-events window.
- `PolicyManagement.Tests` — three focused tenant-isolation tests.

## Prerequisites

- .NET SDK 10
- SQL Server LocalDB, or another SQL Server instance

The default connection string is:

```text
Server=(localdb)\mssqllocaldb;Database=PolicyManagement;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True
```

To use another server, update `ConnectionStrings:PolicyDatabase` in `src/PolicyManagement.Admin/appsettings.json`. For EF commands, set the same value in `POLICYMANAGEMENT_CONNECTION_STRING`.

## Create the database

From the repository root:

```powershell
dotnet restore
dotnet ef database update --project src/PolicyManagement.Data --startup-project src/PolicyManagement.Data
```

The migration seeds two tenants and two policies for each tenant:

| Tenant | Id |
|---|---|
| Bank A | `11111111-1111-1111-1111-111111111111` |
| Bank B | `22222222-2222-2222-2222-222222222222` |

## Run the applications

Start the web host first:

```powershell
dotnet run --project src/PolicyManagement.Admin --launch-profile http
```

It listens on `http://localhost:5101`.

- Blazor UI: `http://localhost:5101`
- Swagger: `http://localhost:5101/swagger`
- API: `GET /api/policies` and `GET /api/policies/{id}`
- WebSocket: `ws://localhost:5101/ws/policies?tenantId={tenantId}`

Then start WPF:

```powershell
dotnet run --project src/PolicyManagement.Desktop
```

The default API and WebSocket URLs already point to port 5101 and can be changed in the WPF window.

## API examples

The API tenant is supplied through `X-Tenant-Id`. Swagger shows this header for both endpoints.

```powershell
curl.exe -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" http://localhost:5101/api/policies
curl.exe -H "X-Tenant-Id: 11111111-1111-1111-1111-111111111111" "http://localhost:5101/api/policies?enabledOnly=true"
```

The API is deliberately read-only. The Blazor UI owns policy writes, as requested.

## See a live event

1. Start the web host and WPF client.
2. In WPF, select **Bank A** and click **Open live events**.
3. Click **Connect / reconnect** in the events window.
4. Open the Blazor UI, select **Bank A**, and create, edit, or delete a policy.
5. The event appears in the WPF events window.

WebSocket messages use this shape:

```json
{
  "eventId": "...",
  "type": "PolicyUpdated",
  "tenantId": "...",
  "policyId": "...",
  "policy": { "id": "...", "name": "Require Screen Lock" },
  "occurredAtUtc": "..."
}
```

## Prove tenant isolation

1. In WPF select Bank A, refresh the API list, and open/connect a Bank A live-events window.
2. Switch the main WPF window to Bank B, refresh again, and open/connect a second live-events window.
3. The two API lists contain different seeded policies.
4. In Blazor select Bank A and change a policy. Only the Bank A event window receives it.
5. Copy a Bank A policy id and call `GET /api/policies/{id}` with the Bank B header. The response is `404`, not the Bank A row.
6. Switch Blazor between Bank A and Bank B. Each tenant sees only its own policies.

## Design answers

### How tenant isolation works

`TenantContext` is scoped. `PolicyDbContext` reads it in an EF Core global query filter, so normal policy queries are tenant-scoped without adding tenant conditions in controllers or pages. `SaveChanges` always replaces the `TenantId` on new tenant-owned rows with the current server tenant and prevents `TenantId` from being changed later.

Blazor stores the selected tenant in the circuit scope. API middleware resolves it from `X-Tenant-Id`. The WebSocket handler resolves it from the connection query string. A wrong-tenant policy id is hidden by the same global filter and returns `404`.

In a real system, the tenant id would come from an authenticated claim. The header/query approach is only a simple tenant-selection mechanism because authentication was outside this assignment.

### Why the data layer is shared

The entities and `PolicyDbContext` are in `PolicyManagement.Data`. The web UI, API controllers, and WebSocket handler all use that one model. There is no duplicated tenant or policy entity.

### Why the API returns DTOs

DTOs keep the public contract separate from EF tracking and navigation properties. They also make it impossible to accidentally expose `TenantId` or accept an EF entity from a client.

### How WebSocket broadcast works

`PolicySocketHub` keeps an in-memory set of sockets grouped by tenant. After a successful Blazor write, it sends the event only to sockets in that tenant group. Sends run independently and a disconnected socket is removed without failing the other clients.

This is intentionally an in-process solution. It has no replay after a client disconnects and assumes one web-host instance.

### How WPF stays responsive

The API view model uses `HttpClient` with `async`/`await`. The events view model uses `ClientWebSocket` and an asynchronous receive loop. Only the small observable-collection update is dispatched back to the WPF UI thread.

### What would change for 10,000 WebSocket clients

I would use SignalR with a Redis or managed backplane, run multiple web instances behind a load balancer, and move policy events to a durable broker. I would also add reconnect/resume tokens and metrics for connection count and send failures.

### First production refactor

I would add authentication and resolve the tenant from validated user claims instead of allowing the caller to select a tenant id. That turns the current isolation mechanism into an actual authorization boundary.

## Tests

```powershell
dotnet test tests/PolicyManagement.Tests
```

The tests check that the global filter hides another tenant's row, a supplied `TenantId` is overwritten on insert, and an existing row cannot be moved to another tenant.

## Deliberate limits

- No authentication, Docker, or deployment scripts because they were not required.
- WebSocket events are in memory and are not replayed.
- Reconnect is a button rather than an automatic retry policy.
- The WPF client uses a small hand-written MVVM implementation to avoid extra framework dependencies.
