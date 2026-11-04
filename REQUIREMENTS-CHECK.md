# Assignment requirements review

Reviewed against the complete Technical_Assignment_Laith.docx, including the general expectations, submission instructions, and seven written questions.

## Required features

| Requirement | Implementation / evidence | Result |
|---|---|---|
| Shared EF Core data library | PolicyManagement.Data; referenced by the single server host | Implemented |
| .NET 10, EF Core and SQL Server | Project targets and SQL Server provider; fresh LocalDB migration and CRUD check | Verified |
| Policy and Tenant fields | Policy.cs, Tenant.cs, PolicySeverity.cs | Implemented |
| Required policy name, maximum 200 characters | PolicyEditModel validation and EF mapping | Implemented |
| Unique policy name | Unique SQL index on TenantId + Name; duplicate validation in service | Verified per tenant |
| Server timestamps | PolicyDbContext.ApplyServerRules | Verified in service tests |
| Two seeded tenants and policies | InitialCreate migration: two tenants, two policies each | Verified on fresh SQL database |
| Tenant-owned row column | Policy.TenantId; required foreign key | Implemented |
| Central tenant query filter | PolicyDbContext global filter | Unit and SQL checks passed |
| Server-selected tenant on save | ApplyServerRules stamps inserts and guards ownership on writes | Unit and SQL checks passed |
| Current-tenant abstraction | ITenantContext; scoped TenantContext for Blazor, API requests and WebSocket connections | Implemented |
| B1: policy grid | Policies.razor | Manually verified |
| B2: validated creation | EditForm, DataAnnotationsValidator, AdminPolicyService | Required-name UI check; CRUD/validation automated |
| B3: edit policy | AdminPolicyService.UpdateAsync | Manual UI and automated SQL check |
| B4: delete with confirmation | JavaScript confirm before AdminPolicyService.DeleteAsync | Code reviewed; delete and event verified on SQL automatically |
| B5: shared persistence | AdminPolicyService uses PolicyDbContext factory | SQL check passed |
| B6: server validation errors | Form error alert from SaveResult; unique index handles concurrent duplicate writes | Code reviewed; duplicate rejection tested |
| B7: tenant selection and isolation | Blazor tenant selector and global filter | Manually and automatically verified |
| A1: async EF reads | PoliciesController.GetAll / GetById | Verified |
| A2: DTO responses | PolicyDto projections | Verified |
| A3: 200 / 404 and problem errors | Controller ProblemDetails; middleware Results.Problem for invalid tenant | HTTP check passed |
| A4: Swagger | AddSwaggerGen, SwaggerUI, tenant-header operation filter | HTTP check passed |
| A5: request tenant isolation | X-Tenant-Id middleware; foreign policy id returns 404 | HTTP and SQL check passed |
| Exactly two policy API endpoints | GET /api/policies and GET /api/policies/{id}; enabledOnly filter | Swagger and HTTP checks passed |
| W1: WebSocket endpoint | /ws/policies | Real ClientWebSocket and WPF checks passed |
| W3: create/update/delete broadcasts | Broadcast after successful AdminPolicyService save | All three verified automatically on SQL and real WebSocket connections |
| W4: JSON event contract | PolicyEventMessage; camelCase serialization | Real WebSocket check passed |
| W5: connection cleanup and slow clients | Handler cleanup and close acknowledgement; per-socket send lock and two-second timeout | Concurrent/failed-client tests; WPF disconnect/reconnect; close-handshake integration check |
| W6: event tenant isolation | Connections grouped by tenant | Real WebSocket check; two WPF windows verified in both directions |
| D1: HTTP and WebSocket clients | HttpClient and ClientWebSocket | Manually verified |
| D2: responsive I/O | async/await; Dispatcher for event collection updates | Code reviewed and manually exercised |
| D3: separation of concerns | Small MVVM view models and commands | Implemented; explained in README |
| D4: configurable URLs | Editable API and WebSocket URL fields | Implemented |
| D5: tenant selection in both protocols | WPF tenant selector; HTTP header and WebSocket query parameter | Manually verified |
| WPF API list, selected details, loading/errors | ApiWindowViewModel and MainWindow | List/details/loading manually verified; error handling code reviewed |
| WPF events and connection state | EventsWindowViewModel and EventsWindow | Live update, disconnect/reconnect and later event manually verified |
| EF migrations in source | InitialCreate and GuardTenantWrites with snapshots | Fresh SQL migration verified |

## README and written answers

README covers connection strings, migration commands, prerequisites, startup order, ports, WPF URLs, exact live-event and isolation steps, and deliberate limits. Its seven design answers cover tenant resolution and enforcement, the shared data layer, DTOs, broadcasts and slow clients, WPF threading, 10,000 clients, and the first production refactor.

## Optional and deliberate limits

- W2 explicitly makes an initial policy list / connected event optional. It is omitted; WPF shows its own connection state.
- The optional Blazor-to-WebSocket change notification is implemented.
- Authentication, Docker, deployment, and elaborate styling are not required by the document. Authentication is not implemented; tenant selection is a demonstration mechanism, not an authorization system.
- Events are in memory, with no replay, and one web-host instance is assumed. These limits are stated in README.
- Automated unit tests use EF InMemory. Separate integration review uses a fresh SQL Server LocalDB database and real network clients. The temporary integration harness lives in the working review folder, not in the submitted source.

All mandatory requirements have an implementation. Evidence above distinguishes manual checks, automated checks, and code review; it does not claim every UI edge case was manually exercised.
