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
- No test project

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
- Frontend keeps the access token in memory (signal); the refresh token lives in an httpOnly, Secure, SameSite=Strict cookie with Path=/api/auth
- Development: Angular proxy (frontend/proxy.conf.json) forwards /api to the API, so calls are same-origin
- Rate limits (built-in RateLimiting): global 100/min per IP; auth endpoints 5/min per IP; AI 10/min per user
- Users can see only projects they own or are members of; return 404 (not 403) for resources the user cannot see

### Role permissions
- Admin: everything, including user management (list users, change roles, deactivate)
- ProjectManager: can create projects; on projects they own, can update, delete, manage members, and create/edit/assign/delete tasks and update task status. On projects where they are only a member, they have read access. They get read-only user lookup for assigning tasks
- TeamMember: read access to projects they are a member of; can update status only on tasks assigned to them
- AI improve-description endpoint: Admin and ProjectManager only
- Dashboard is scoped to the projects the user can see (Admin sees global stats)

### Task rules
- Any status transition is allowed, except that leaving Cancelled means reopening to ToDo, which only a PM or Admin can do
- Moving to Completed sets CompletedAt; reopening clears it
- Dashboard: Pending = ToDo + InProgress; Cancelled counts as neither completed nor pending; Upcoming = due within 7 days and not Completed/Cancelled; also show an Overdue count

### AI
- GitHub Models via its OpenAI-compatible endpoint (e.g. gpt-4o-mini); PAT kept in user-secrets
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

## Workflow
- Explain the plan before large changes
- Keep commits small and focused