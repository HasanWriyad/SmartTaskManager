# Smart Task Management System

## Architecture
Clean Architecture:
- Domain: entities, enums, no dependencies
- Application: DTOs, interfaces, services, FluentValidation validators
- Infrastructure: EF Core (SQL Server), repositories, JWT, AI service
- API: thin controllers, middleware, DI setup

## Rules
- .NET 10 (LTS), EF Core 10, C# 14; all Microsoft packages on 10.x
- Target SQL Server 2019 (LocalDB): set UseCompatibilityLevel(150) in UseSqlServer
- async/await everywhere, CancellationToken in all async methods
- Swagger UI via Swashbuckle.AspNetCore (OpenAPI document with JWT bearer support)
- Controllers contain no business logic
- All responses wrapped in ApiResponse<T>
- Lists use PagedResult<T> with search, filter, sort, page, pageSize
- No secrets in appsettings.json; use user-secrets / environment variables
- Passwords hashed with ASP.NET Core Identity PasswordHasher
- Roles: Admin, ProjectManager, TeamMember (policy-based authorization)
- Log with Serilog; never log tokens or passwords
- No test project. End-to-end API check scripts live in tools/api-checks/ (run tools/api-checks/Run-ApiChecks.ps1; they use a throwaway database)

## Frontend (frontend/)
- Angular 22, standalone components only, lazy-loaded routes
- Angular 22 conventions: signals for state, new control flow (@if, @for), inject() over constructor injection
- Reactive forms, functional guards and interceptors
- Services handle HTTP; components stay thin

## Decisions
### Data model
- Single role per user: `Role` enum column on User, emitted as the JWT `role` claim
- Entities: User, RefreshToken, Project, ProjectMember, TaskItem
- ProjectMember (ProjectId, UserId) with simple add/remove member endpoints; a task's assignee must be a project member
- Creating a project adds the owner as a member in the same save; removing the owner from the members returns 409
- Removing a member unassigns their tasks in that project (same save); adding an inactive user or an existing member returns 409
- Soft delete for Project and TaskItem (IsDeleted, DeletedAt, DeletedById) via EF Core 10 named query filters plus a SaveChanges interceptor; deleting a project soft-deletes its tasks in the same transaction
- No soft delete for RefreshToken (revoke and purge), ProjectMember (hard delete) or User (deactivate with IsActive)
- No restore or trash endpoints in v1
- No RowVersion / optimistic concurrency
- Project name unique per owner (filtered unique index WHERE IsDeleted = 0)
- Database: `UseCompatibilityLevel(150)` only; never run ALTER DATABASE to change the compatibility level
- Keyword search uses LIKE (LocalDB has no full-text search)

### Auth and security
- Public registration always creates a TeamMember; Admin changes roles
- Seed one Admin at startup if none exists; credentials come from user-secrets / environment variables
- Access token lasts 15 min, refresh token 7 days, rotated on every refresh, stored as a SHA-256 hash; reusing a revoked token revokes its chain
- Logout revokes the refresh token; no access-token blacklist
- Refresh tokens carry a FamilyId (one per login); revoking a chain means revoking the family
- Register returns the created user (201) and no tokens; the client calls login afterwards
- D6: role changes and deactivation take effect immediately. Every authenticated request checks the token against the user's current IsActive and Role (one primary-key lookup in the JWT OnTokenValidated event, Api/Auth/AccessTokenStateValidator.cs); a deleted, inactive or role-mismatched user gets 401, so the client refreshes and receives a token with the current role (refresh fails for inactive users). Still no access-token blacklist; logout revokes the refresh token
- Frontend keeps the access token in memory (signal); the refresh token lives in an httpOnly, Secure, SameSite=Strict cookie with Path=/api/auth
- The Angular app must share a single in-flight refresh request across the interceptor and session restore, because parallel refreshes count as token reuse
- Development: Angular proxy (frontend/proxy.conf.json) forwards /api to the API, so calls are same-origin
- Rate limits (built-in RateLimiting): global 100/min per IP; auth endpoints 5/min per IP; AI 10/min per user
- D15: the strict 5/min "auth" policy applies to login and register only (the brute-force targets). Refresh and logout stay under the global limit, because session restore calls refresh on every page load and a user reloading a few times a minute would otherwise lock themselves out
- Rate limits partition by the connection's IP: the README must tell deployments behind a reverse proxy or load balancer to configure ForwardedHeaders, otherwise all clients share the proxy's IP and one budget
- Users can see only projects they own or are members of; return 404 (not 403) for resources the user cannot see

### Role permissions
- Admin: everything, including user management (list users, change roles, deactivate)
- ProjectManager: can create projects; on projects they own, can update, delete, manage members, and create/edit/assign/delete tasks and update task status. On projects where they are only a member, they have read access, except updating the status of tasks assigned to them. They get read-only user lookup for assigning tasks
- TeamMember: read access to projects they are a member of; can update status only on tasks assigned to them
- Any active assignee, whatever their role, can update the status of their own task; reopening a cancelled task stays limited to the project owner or an Admin
- AI improve-description endpoint: Admin and ProjectManager only
- Dashboard is scoped to the projects the user can see (Admin sees global stats)

### Task rules
- Any status transition is allowed, except that leaving Cancelled means reopening to ToDo, which only a PM or Admin can do
- Moving to Completed sets CompletedAt; reopening clears it
- Dashboard: Pending = ToDo + InProgress; Cancelled counts as neither completed nor pending; Upcoming = due within 7 days and not Completed/Cancelled; also show an Overdue count

### AI
- D10: Google Gemini via its OpenAI-compatible endpoint (https://generativelanguage.googleapis.com/v1beta/openai/), called with the OpenAI .NET SDK; API key in user-secrets as AiSettings:ApiKey
- Model: gemini-3.5-flash-lite (AiSettings:Model). Older models (gemini-2.5-flash, gemini-2.5-flash-lite) are not available to new API keys, and the newest ones (gemini-3.7-flash, gemini-3.8-flash) were overloaded or slow during testing in November 2026. gemini-3.5-flash gave good results but took 20-28 s per request; gemini-3.5-flash-lite gave comparable quality in 1.5-3 s on the same three live examples, so it is the default
- Timeout 30 s per request (AiSettings:TimeoutSeconds); one retry after ~1 s on a provider 503 only, within that timeout; no retry on timeouts, 429 or other errors
- AiSettings:ReasoningEffort = "minimal" (sent as reasoning_effort; empty means not sent) and AiSettings:MaxOutputTokens = 1500. Reason: gemini-3.5-flash thinks by default, and its hidden thinking tokens (~574 of 600) consumed the output budget, so answers were cut off (finish_reason "length"). gemini-3.5-flash-lite rejects "none" (400 invalid argument) but accepts "minimal", which used no thinking tokens in testing
- GitHub Models (the original choice) was retired on 30 July 2026, so it is no longer an option
- When no key is configured, a stub implementation is used instead
- The endpoint returns a suggestion only and does not save it; the user applies it through a normal task update
- Input capped at ~2000 characters; output validated (non-empty, length-capped, plain text)

### Conventions
- Plain application services; no MediatR or AutoMapper (manual mapping extensions)
- Per-aggregate repositories with DbContext as the unit of work; no generic repository
- Sort fields are whitelisted
- Routes are plain /api/... with no versioning
- TreatWarningsAsErrors only for Release builds
- Frontend UI: Angular Material
- Deliverables: idempotent migration script at docs/database.sql, plus a Postman collection and swagger.json in docs/

## Design
- The UI follows docs/design/DESIGN-SPEC.md (and the screenshots next to it) for look and feel
- Rule: the frontend implements what we planned and what the backend already does; the design spec only decides how it looks. If part of the spec needs an endpoint, field or behaviour the backend does not have, follow the backend and the decisions above, leave that part out and list it. Do not invent features
- Overrides (already decided):
  - Auth stays as decided: access token in memory, refresh token in the httpOnly cookie. Never store tokens in localStorage
  - Login: no "Keep me signed in" and no "Forgot password?" (no backend). Keep the "Create an account" link to the register page
  - AI suggestion panel: show and apply only the improved description. No suggested title, priority or "What changed" tags
  - The task dialog saves through the existing endpoints (fields, status and assignee are separate calls); only send what changed
- Notes for later pages:
  - Task list search placeholder is "Search tasks" (the search matches title and description, not project names)
  - The dashboard's my-tasks card is labelled "My open tasks" (the API returns only the caller's pending and overdue counts)
- Branding: the SmartTask mark is shared/logo/logo-mark.ts (sidebar, auth top bar, auth brand panel); the favicons in frontend/public are its simplified form (tile and bold check)

## Workflow
- Explain the plan before large changes
- Keep commits small and focused
- Only stop processes you started yourself, by PID.