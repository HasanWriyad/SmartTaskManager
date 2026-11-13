# Requirements checklist (backend)

Every backend requirement from the assignment, checked against the code on the `main` branch.

**Status:** ✅ Done · ⚠️ Partial · ❌ Missing

Paths are relative to `src/` unless they start with `docs/` or `tools/`. Endpoints are under `/api` unless noted.

## Technology stack

| Requirement | Status | Where |
|---|---|---|
| ASP.NET Core 9 or 10 | ✅ | .NET 10 (`Directory.Build.props`, `global.json` pins SDK 10.0.x) |
| Entity Framework Core | ✅ | EF Core 10 — `SmartTaskManager.Infrastructure/Persistence/AppDbContext.cs` |
| SQL Server | ✅ | SQL Server 2019 LocalDB, `UseCompatibilityLevel(150)` — `SmartTaskManager.Infrastructure/DependencyInjection.cs` |
| JWT authentication | ✅ | `SmartTaskManager.Api/Extensions/AuthExtensions.cs`, `SmartTaskManager.Infrastructure/Auth/JwtTokenService.cs` |
| Swagger / OpenAPI | ✅ | Swashbuckle with JWT bearer — `SmartTaskManager.Api/Extensions/SwaggerExtensions.cs`; export in `docs/swagger.json` |

## Authentication and authorization

| Requirement | Status | Where |
|---|---|---|
| User registration | ✅ | `POST /auth/register` — always creates a TeamMember (`Application/Features/Auth/AuthService.cs`) |
| Login | ✅ | `POST /auth/login` |
| Logout | ✅ | `POST /auth/logout` — revokes the session's refresh token family and clears the cookie |
| JWT authentication | ✅ | 15-minute access token in the response body |
| Refresh tokens | ✅ | `POST /auth/refresh` — httpOnly/Secure/SameSite=Strict cookie, stored as SHA-256 hash, rotated on every use, reuse revokes the whole family (`Infrastructure/Repositories/RefreshTokenRepository.cs`) |
| Role-based authorization | ✅ | Policies `AdminOnly`, `AdminOrProjectManager`, fallback `AnyUser` (`Api/Authorization/Policies.cs`); ownership and membership rules in `Application/Features/Projects/ProjectAccess.cs` and `Tasks/TaskAccess.cs`; every token is checked against the user's current role and active flag, so changes apply immediately (`Api/Auth/AccessTokenStateValidator.cs`) |
| Roles Admin, Project Manager, Team Member | ✅ | `Domain/Enums/UserRole.cs`; user and role management in `GET /users`, `PUT /users/{id}/role`, `PUT /users/{id}/status` |
| Each role has appropriate permissions | ✅ | Role-permission matrix in `CLAUDE.md` (Decisions → Role permissions), enforced in the services |

## Project management

| Requirement | Status | Where |
|---|---|---|
| Create project | ✅ | `POST /projects` (Admin, ProjectManager) |
| Update project | ✅ | `PUT /projects/{id}` (Admin, owning ProjectManager) |
| Delete project | ✅ | `DELETE /projects/{id}` — soft delete, cascades to tasks in the same transaction |
| View project details | ✅ | `GET /projects/{id}` — includes members; 404 when not visible |
| List projects | ✅ | `GET /projects` — only projects the caller can see |
| Search projects | ✅ | `search` on name and description; filters `ownerId`, `ownedByMe`, `createdFrom`/`createdTo` |
| Sort and paginate results | ✅ | `sortBy` = name, createdAt, taskCount; `page`, `pageSize` → `PagedResult<T>` |

Also implemented: project members (`POST /projects/{id}/members`, `DELETE /projects/{id}/members/{userId}`).

## Task management

| Requirement | Status | Where |
|---|---|---|
| Each project contains multiple tasks | ✅ | `/projects/{projectId}/tasks` |
| Create task | ✅ | `POST /projects/{projectId}/tasks` |
| Update task | ✅ | `PUT /projects/{projectId}/tasks/{taskId}` |
| Delete task | ✅ | `DELETE /projects/{projectId}/tasks/{taskId}` (soft delete) |
| Assign task | ✅ | `PUT …/{taskId}/assignee`, `DELETE …/{taskId}/assignee`; the assignee must be an active project member |
| Update status | ✅ | `PUT …/{taskId}/status` — transition rules and `CompletedAt` in `Domain/Entities/TaskItem.cs` |
| Set priority | ✅ | `priority` on create and update |
| Due date | ✅ | `dueDate` on create and update (no past dates when set or changed) |
| Search tasks | ✅ | `search` on title and description |
| Sort and paginate tasks | ✅ | `sortBy` = title, priority, status, dueDate, createdAt; paging |
| Status values To Do, In Progress, Completed, Cancelled | ✅ | `Domain/Enums/TaskItemStatus.cs` |
| Priority values Low, Medium, High, Critical | ✅ | `Domain/Enums/TaskPriority.cs` |

## Dashboard

| Requirement | Status | Where |
|---|---|---|
| Total projects | ✅ | `GET /dashboard` → `totalProjects` |
| Total tasks | ✅ | `totalTasks` |
| Tasks by status | ✅ | `tasksByStatus` (all four statuses, including zero counts) |
| Tasks by priority | ✅ | `tasksByPriority` |
| Completed vs pending tasks | ✅ | `completion` (completed, pending = To Do + In Progress, cancelled) |
| Upcoming due tasks | ✅ | `upcomingCount` and `upcomingTasks` (pending, due within 7 days) |

Also implemented: overdue count and a "my tasks" summary. Data is scoped to the projects the caller can see; three SQL queries per request (`Infrastructure/Repositories/DashboardRepository.cs`).

## Search, sorting and pagination

| Requirement | Status | Where |
|---|---|---|
| Keyword search | ✅ | Projects, tasks, users, user lookup |
| Filtering | ✅ | Projects: ownerId, ownedByMe, createdFrom/createdTo. Tasks: status, priority, assigneeId, dueFrom/dueTo. Users: role, isActive |
| Sorting | ✅ | Whitelisted `sortBy` per list (`Application/Common/Paging/PagedQueryValidator.cs`) |
| Pagination | ✅ | `PagedQuery` / `PagedResult<T>` with totals and next/previous flags |

## AI feature

| Requirement | Status | Where |
|---|---|---|
| Endpoint that improves task descriptions | ✅ | `POST /ai/improve-description` (Admin, ProjectManager); returns a suggestion only |
| Correct grammar | ✅ | System prompt — `Infrastructure/Ai/DescriptionPrompts.cs` |
| Improve clarity | ✅ | System prompt |
| More professional | ✅ | System prompt |
| Expand short descriptions | ✅ | System prompt (without inventing facts) |
| Actionable task descriptions | ✅ | System prompt requires a `Steps:` list of 3–7 steps |
| GitHub Models (free) or any free AI provider | ✅ | Google Gemini (`gemini-3.5-flash-lite`) via its OpenAI-compatible endpoint. GitHub Models was retired on 30 July 2026. Offline fallback when no API key is set. Live examples in `docs/ai-examples.md` |

## Architecture

| Requirement | Status | Where |
|---|---|---|
| N-Layer or Clean Architecture | ✅ | Clean Architecture: Domain → Application → Infrastructure → Api; Domain has no dependencies, and Application references no EF Core or ASP.NET Core |

## Security

| Requirement | Status | Where |
|---|---|---|
| JWT authentication | ✅ | See above |
| Refresh tokens | ✅ | See above |
| Password hashing | ✅ | ASP.NET Core Identity `PasswordHasher` (PBKDF2) — `Infrastructure/Auth/PasswordHasher.cs` |
| Role-based authorization | ✅ | See above |
| HTTPS | ✅ | `UseHttpsRedirection`, HSTS outside Development (`Api/Program.cs`); refresh cookie is Secure |
| CORS | ✅ | Named policy with configured origins and credentials (`Api/Extensions/ApiExtensions.cs`) |
| Input validation | ✅ | FluentValidation for every body and query DTO, run by a global filter (`Api/Filters/ValidationFilter.cs`); 1 MB request body limit |
| Basic rate limiting | ✅ | Global 100/min per IP, login/register 5/min per IP, AI 10/min per user (`Api/RateLimiting/RateLimitingSetup.cs`) |
| Secure configuration | ✅ | No secrets in `appsettings*.json`; JWT key, admin seed and AI key come from user-secrets or environment variables; options validated at startup |

## Backend expectations

| Requirement | Status | Where |
|---|---|---|
| Async/await | ✅ | All I/O is async with `CancellationToken`; no blocking calls (`.Result`, `.Wait()`) |
| FluentValidation | ✅ | `Application/Features/*/…Validators.cs`, `…Query.cs` |
| Swagger | ✅ | Development only; JWT bearer authorize button |
| Global exception handling | ✅ | `Api/ErrorHandling/GlobalExceptionHandler.cs` (+ status-code pages for empty 401/403/404/405/413/415) |
| Logging (Serilog) | ✅ | Console and rolling file (`Api/appsettings.json`); tokens, passwords, keys and user text are not logged |
| Health checks | ✅ | `GET /health` (no `/api` prefix) — anonymous, database check, 503 when unhealthy |
| Consistent API responses | ✅ | `ApiResponse<T>` for success and every error, including 401, 403, 404, 413, 415, 429 and model-binding errors |

## Deliverables (backend part)

| Requirement | Status | Where |
|---|---|---|
| Source code in a GitHub repository | ✅ | `origin` = github.com/HasanWriyad/SmartTaskManager |
| SQL database script or EF Core migrations | ✅ | `Infrastructure/Persistence/Migrations/` and `docs/database.sql` (idempotent) |
| API collection (Postman or Swagger export) | ✅ | `docs/SmartTaskManager.postman_collection.json`, `docs/swagger.json` (regenerate with `tools/export-api-docs.ps1`) |

## Not backend — status for completeness

| Requirement | Status | Note |
|---|---|---|
| Angular frontend (login, dashboard, projects, tasks, search, responsive UI, lazy loading, guards, interceptors, reactive forms) | ❌ | Next step |
| README.md (overview, setup, stack, API overview, folder structure) | ❌ | `README.md` is a one-line placeholder |
| PROMPTS.md (prompt design, structure, examples, validation, safety) | ❌ | Source material ready in `docs/ai-examples.md` |

## Gaps

1. **README.md and PROMPTS.md** are missing (documentation deliverables).
