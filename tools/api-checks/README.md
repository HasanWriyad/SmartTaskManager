# API checks

End-to-end checks that call the running API over HTTPS and verify responses, database state and logs. They complement Swagger and the Postman collection. They are not a unit-test project: the project rules say there is no test project, so these are PowerShell scripts.

## What a run does

1. Builds the API into a temporary folder (`%TEMP%\stm-api-checks\api`), never the project's `bin`. A copy of the API you have open in Visual Studio or `dotnet run` is not disturbed.
2. Creates a **throwaway database** for the run (`SmartTaskManager_ApiChecks_<random>`). The API creates and migrates it on startup, and the runner drops it at the end, even when a check fails. Your development database is never touched.
3. Starts the API on port 7300 once per configuration group (for example rate limits lowered, or the AI endpoint pointed at a local fake server). It stops only the processes it started.
4. Prints a `PASS`/`FAIL` line per check and a summary, and **exits with code 1 if any check fails**, so it can gate a CI job.

## Prerequisites

- Windows with PowerShell 5.1 or 7, `curl.exe` (built into Windows 10 and later) and `sqlcmd`
- .NET 10 SDK and SQL Server LocalDB (`MSSQLLocalDB`)
- User-secrets set up: run `tools/setup-dev.ps1` once. The checks sign in as the seeded Admin, so they read `SeedAdmin:Email` and `SeedAdmin:Password` from user-secrets. No AI key is needed: AI suites use a local fake server or the offline fallback.

## Running

From the repository root:

```powershell
./tools/api-checks/Run-ApiChecks.ps1                       # all suites except ai-live (about 10 minutes)
./tools/api-checks/Run-ApiChecks.ps1 -Suite auth, tasks    # selected suites
./tools/api-checks/Run-ApiChecks.ps1 -Live                 # also call the real AI provider (uses API quota)
./tools/api-checks/Run-ApiChecks.ps1 -KeepDatabase         # keep the throwaway database for debugging
```

## Suites

| Suite | Covers |
|---|---|
| `auth` | Register, login, refresh rotation, reuse detection, concurrent refresh, logout, inactive users, cookie attributes, hashed storage |
| `users` | Admin user list and lookup, role and status changes, self-protection, last-Admin rule and race, immediate effect of role changes and deactivation |
| `projects` | CRUD, role matrix (404 vs 403), members, soft delete cascading to tasks in one transaction, rollback on failure |
| `tasks` | CRUD, assignment rules, status transitions and `CompletedAt`, assignee permissions, due-date rules, soft delete |
| `lists` | Search, filters, sorting and paging for projects, project tasks and all visible tasks (`GET /api/tasks`, incl. projectId and overdue); validation of query parameters |
| `dashboard` | Every statistic per role, exclusions, and the query shape (number of SQL commands, `GROUP BY`) |
| `ai-fallback` | AI endpoint with no key configured: offline fallback |
| `ai-provider` | AI endpoint against a local fake OpenAI-compatible server: request shape, prompt injection handling, 429/500/503/timeout/invalid-output handling, one retry on 503 |
| `ai-no-reasoning-effort` | An empty `AiSettings:ReasoningEffort` sends no `reasoning_effort` |
| `rate-limits` | Global, auth and AI limits with 429 responses; `/health`; the 1 MB body limit |
| `health` | `/health` turns 503 when the database disappears (the suite drops its own throwaway database) |
| `ai-live` | Opt-in (`-Live`): three requests to the real provider, with outputs and response times |

## Files

| File | Purpose |
|---|---|
| `Run-ApiChecks.ps1` | Runner: build, throwaway database, API per configuration group, summary and exit code |
| `ApiChecks.psm1` | Shared helpers: `Req` (HTTP), `Sql`, `Login`, `Check`, suite bookkeeping |
| `fake-openai.ps1` | Local OpenAI-compatible fake server used by the AI suites |
| `suites/*.ps1` | One file per suite |

## Writing a suite

Start with `$tmp = Start-Suite '<name>'`, use `Req`, `Sql`, `Login` and `Check`, end with `Complete-Suite`, and add the suite to a group in `Run-ApiChecks.ps1`. Suites may create any data they need: the database is discarded after the run.
