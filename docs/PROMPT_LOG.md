# Prompt Log

This log records how Claude Code was used to build the Smart Task Management
System: each prompt, what it produced, and what I reviewed or changed.
Project rules and decisions are kept in CLAUDE.md, which Claude Code reads
on every step.

---

### Step 0 — Architecture Planning
**Prompt:**
Read CLAUDE.md and the assignment requirements in docs/assignment.pdf.
Propose the solution structure, entity relationships, and a role-permission
matrix for Admin, ProjectManager, and TeamMember. Also recommend whether to use
soft delete for projects and tasks, and list any decisions I need to make.
Do not write code yet.

**Result:** Clean Architecture solution structure, entity model with
ProjectMember for project membership, role-permission matrix, soft delete
design, and 16 open decisions.

**My review/changes:** Reviewed the plan before any code was written. Agreed
with avoiding MediatR and AutoMapper (commercial licences) and with returning
404 instead of 403 for resources a user cannot see.

---

### Step 0.1 — Plan Update After CLAUDE.md Changes
**Prompt:**
I updated CLAUDE.md: the Angular app goes in frontend/, and the database
targets SQL Server 2019 LocalDB with compatibility level 150. Also, do not
include a test project. Re-read CLAUDE.md and update your plan only where
these changes affect it. Do not write code yet.

**Result:** Plan updated for SQL Server 2019 (compatibility level, LIKE-based
search, idempotent SQL script) and a new decision D17 about altering the
database compatibility level.

**My review/changes:** Rejected D17. Setting UseCompatibilityLevel(150) in EF
is enough, and ALTER DATABASE inside a migration transaction can fail.

---

### Step 0.2 — Architecture Decisions
**Prompt:**
Decisions:
- D1, D2, D4, D6, D7, D8, D9, D10, D11, D12, D13, D14, D15, D16: accept your recommendations.
- D3: TeamMember can only update status on assigned tasks. The AI improve
  endpoint is limited to Admin and ProjectManager.
- D5: access token in memory, refresh token in an httpOnly, Secure,
  SameSite=Strict cookie scoped to /api/auth. In development, use an Angular
  proxy (proxy.conf.json) so the frontend calls /api on the same origin.
- D17: no. UseCompatibilityLevel(150) is enough; do not alter the database.
- Drop RowVersion / optimistic concurrency to keep scope small.
- TreatWarningsAsErrors only for Release builds.
- Keep ProjectMember, with simple add/remove member endpoints.

Add these decisions to CLAUDE.md under a "## Decisions" section, then wait.
Do not write code yet.

**Result:** Decisions recorded in CLAUDE.md.

**My review/changes:**
- Found that a SameSite=Strict refresh cookie would not be sent in
  development, because the Angular dev server (http) and the API (https) are
  treated as different sites. Solved with an Angular dev proxy.
- Restricted the AI endpoint to roles that can edit descriptions, so the
  permission rules stay consistent.
- Reduced scope for the deadline by dropping optimistic concurrency.

---

### Step 2.1 — Solution, Domain and Database
**Prompt:**
Scaffold the solution as planned: SmartTaskManager.slnx, Directory.Build.props,
Directory.Packages.props, and the four projects. Use EF Core code-first.
Add all Domain entities and enums from the approved plan, AppDbContext with
entity configurations, the soft delete interceptor and named query filters,
and create the initial migration with dotnet ef. Seed one Admin user at
startup if none exists, with credentials from user-secrets. Make sure the
solution builds and the database is created.

**Result:** Solution scaffold with central package management (10.0.15),
domain entities with encapsulated rules, soft delete interceptor with named
query filters, initial migration, and an idempotent Admin seeder using
user-secrets.

**My review/changes:** Verified the Debug and Release builds, database
creation at compatibility level 150, and that seeding runs only once.
Accepted the change from Cascade to Restrict on ProjectMember, because a
database cascade would hard-delete member rows before the soft delete
interceptor runs. Confirmed docs/assignment.pdf stays gitignored after the
.gitignore rewrite.

---

### Step 2.2 — Cross-cutting Infrastructure
**Prompt:**
Add ApiResponse<T>, the global exception handler mapping exceptions to
consistent responses, Serilog (console and rolling file), Swagger UI with
Swashbuckle and JWT bearer support, CORS, HTTPS redirection, ICurrentUser,
and the authorization policies from the plan.

**Result:** Consistent ApiResponse body with traceId for every error,
including framework 401/403/405 responses. Exception mapping to 400/404/409/
422/500, Serilog to console and rolling file, Swagger with Bearer support
(Development only), CORS for the Angular origin with credentials, JWT bearer
validation, and a fallback policy requiring authentication by default.

**My review/changes:** Verified each behaviour with real requests: 401 for
missing or invalid tokens, 403 for wrong roles, correct status codes per
exception type, no internal details in responses, and CORS limited to the
Angular origin. Accepted pinning Swashbuckle to 10.3.1 instead of a release
published two days earlier. Noted that the health endpoint must be marked
anonymous later because of the default authentication policy.

---

### Step 2.3 — Authentication
**Prompt:**
Implement register (always creates a TeamMember), login, refresh, and logout.
Return the access token in the response body and set the refresh token in an
httpOnly, Secure, SameSite=Strict cookie scoped to /api/auth. Store refresh
tokens hashed, rotate on every refresh, revoke the whole chain if a revoked
token is reused, and block inactive users. Add FluentValidation for all inputs.
Test the full flow in Swagger.

**Result:** Register, login, refresh and logout endpoints. Access token
(15 min) in the body, refresh token in a secure httpOnly cookie scoped to
/api/auth, SHA-256 hashed storage, rotation on every refresh, and reuse
detection that revokes the whole token family. 34 automated flow checks passed.

**My review/changes:**
- Verified that register ignores a role sent by the client, that login
  errors do not reveal whether an email exists or an account is inactive,
  and that no tokens or passwords appear in logs.
- Accepted a FamilyId column so a token family can be revoked in one update.
- Kept register and login separate, and accepted that inactive users keep
  their access token until it expires (no blacklist, per decision D6).
- Recorded in CLAUDE.md that the Angular app must share one in-flight
  refresh request, because parallel refreshes are treated as token reuse.
- Tested the flow manually in Swagger UI and checked the cookie attributes
  in browser DevTools.



  ---

### Step 2.4 — User Management
**Prompt:**
Add Admin-only endpoints to list users, change a user's role, and activate or
deactivate a user. An Admin cannot deactivate themselves or change their own
role, and the last active Admin cannot be demoted or deactivated. Add a
read-only user lookup endpoint that Admin and ProjectManager can use when
adding project members or assigning tasks. Never return password hashes.

**Result:** Admin endpoints for listing users (search, filter, sort, paging),
changing roles and activating or deactivating users, plus a lookup endpoint
for Admin and ProjectManager that returns only id, name and email. Reusable
paging components (PagedQuery, PagedResult, sort whitelist validator).

**My review/changes:**
- Added the self-protection and last-Admin rules to the prompt myself, so the
  system can never be left without an active Admin.
- Verified that concurrent demotions are serialised with a SQL Server
  application lock, so one Admin always remains.
- Verified that deactivation revokes all refresh tokens immediately and that
  password hashes are never read or returned.
- Accepted that a role change applies on the user's next token refresh
  (at most 15 minutes), consistent with decision D6.


  ---

### Step 3.1 — Project Module
**Prompt:**
Implement the Projects feature: create, update, soft delete (also soft-deletes
its tasks in the same transaction), get details, and list. Enforce the
role-permission matrix in the service layer using ICurrentUser, and return 404
for projects the user cannot see. Add endpoints to add and remove project
members. Keep the list endpoint simple for now; search, sorting, and pagination
come in a later step. Test the soft delete interceptor and the cascade to tasks.

**Result:** Project CRUD and member management. Permission rules centralised
in ProjectAccess, used both for SQL filtering and the canManage flag.
404 for projects a user cannot see, 403 for projects they can see but not
change. Soft delete cascades to live tasks in a single transaction.

**My review/changes:**
- Added the soft delete test to the prompt, since it could not be verified
  in step 2.1.
- Verified the cascade, the single transaction and a forced rollback.
- Accepted the fix that limits soft delete updates to the delete columns, so a
  concurrent edit is not overwritten.
- Accepted that removing a member unassigns their tasks in that project,
  which keeps the "assignee must be a member" rule true without blocking
  the removal.
- Added global.json to pin the .NET 10 SDK after a newer SDK appeared on the
  machine, so builds stay reproducible for reviewers.
- Added a workflow rule after Claude Code stopped my running API instance:
  only stop processes it started itself.

  ---

### Step 4.1 — Task Module
**Prompt:**
Implement the Tasks feature inside projects: create, update fields, soft
delete, get details, list, assign or reassign (assignee must be a project
member), update status, set priority, and due date. Apply the status transition
rules and CompletedAt handling from the decisions. TeamMember can only update
the status of tasks assigned to them. Keep the list endpoint simple for now.

Decisions:
1. Change it: any active assignee can update the status of their own task,
   whatever their role. Reopening a cancelled task stays limited to the owner
   or an Admin.
2. Change it: when a project is created, add the owner as a member in the same
   save. Removing the owner from the members returns 409. Update existing
   projects in the dev database if needed.
3. Keep priority and due date on the main PUT.



**Result:** Task CRUD under projects, assignment limited to active project
members, status workflow with CompletedAt handling and cancelled-task rules,
due date validation, and canManage/canUpdateStatus flags for the UI. Project
visibility check refactored into a single shared query.

**My review/changes:**
- Changed the status rule to be based on the assignee rather than the role:
  following the matrix literally meant a ProjectManager assigned to a task
  in someone else's project could not complete it.
- Made the project owner a member automatically on creation and blocked
  removing the owner, so a manager can assign tasks to themselves.
- Kept priority and due date on the main update endpoint.
- Verified that omitted fields cannot silently reset values (priority on
  update, assignee on an empty body).
  - Verified that a user deactivated mid-session cannot change task status even
  with a still-valid token, because the active check reads the database.

---

### Step 5.1 — Search, Sorting and Pagination
**Prompt:**
Apply PagedQuery and PagedResult<T> to the project and task list endpoints.
Projects: search by name and description, sort by name, createdAt, and task
count. Tasks: search by title and description, filter by status, priority,
assignee, and due date range, sort by title, priority, status, dueDate, and
createdAt. Keep the current default orders. Use the existing sort whitelist
validator.

**Result:** Both lists return PagedResult with search, filters and
whitelisted sorting. Validation returns 400 for unknown sort fields, page
sizes outside 1–100, invalid enums or dates, and an inverted due date range.

**My review/changes:**
- Reused the paging components built in step 2.4 instead of new ones.
- Verified that unknown sort fields and oversized page sizes are rejected.
- Accepted stable tie-breaking (creation time, then id) so paging does not
  repeat or skip items between requests.
- Accepted that a page past the end returns an empty list with real totals
  rather than 404.

---

### Step 6.1 — Dashboard
**Prompt:**
Add a dashboard endpoint returning total projects, total tasks, tasks by
status, tasks by priority, completed vs pending, upcoming due tasks (next 7
days), and overdue count, using the definitions in the decisions. Scope the
data by role as in the matrix. Use efficient grouped queries instead of
loading entities into memory.

**Result:** GET /api/dashboard with totals, counts by status and priority
(including zeros), completed/pending/cancelled, overdue and upcoming counts,
and the 10 soonest upcoming tasks. Scoped through the same visibility rule
as the project list. Three SQL commands per request, with no full task rows
loaded.
**My review/changes:**
- Verified the query count in the SQL log and that Admin figures match an
  independent SQL query.
- Found that the "my tasks" view for team members from the original plan was
  missing (see step 6.2).
- Accepted UTC as the definition of "today", consistent with due date
  validation, and noted it for the README.

  ---

### Step 6.2 — Dashboard: My Tasks
**Prompt:**
Add a small "my tasks" section for the caller: counts of their own pending
and overdue tasks (assigned to them, within visible projects). Keep it
within the existing grouped query if possible. Then rerun the dashboard
suite and commit this step.

**Result:** myTasks { pending, overdue } added to the dashboard response,
computed as an extra key in the existing GROUP BY. Still three SQL commands
per request.

**My review/changes:**
- Asked for this to keep the team member's dashboard useful, since project-wide
  totals do not show their own workload.
- Verified that the per-role counts exclude completed and soft-deleted tasks.
  

  ---

### Step 7.1 — AI Description Improvement
**Prompt:**
Implement IAiDescriptionService with GitHub Models through the OpenAI .NET SDK
(endpoint https://models.github.ai/inference, model from configuration, token
from user-secrets AiSettings:GitHubToken). Add POST /api/ai/improve-description
for Admin and ProjectManager only. The system prompt must correct grammar,
improve clarity, make it professional, expand short text, and produce
actionable steps. Cap input at about 2,000 characters, treat user text as data
not instructions, set a timeout, validate the output, handle 429 and other
failures with a clear message, and use the fallback stub when no token is set.
Do not save anything; return a suggestion only.

**Result:** Provider-neutral service using the OpenAI chat API against a
configurable endpoint. User text wrapped in delimiter tags, with embedded tags
stripped first. Output validation (empty, truncated, oversized, filtered or
containing delimiters). Distinct errors for rate limits (429), provider
failures (503), unusable output (502) and timeouts (504). Offline fallback
stub when no key is configured.

**My review/changes:**
- Reviewed the system prompt against the five requirements in the assignment
  and the prompt injection rules.
- Asked for a live test with three cases (vague, messy, injection attempt)
  so the real outputs could be used in PROMPTS.md.
- The live test showed that GitHub Models was retired on 30 July 2026, so the
  provider had to change (see step 7.2).

---

### Step 7.2 — Switch Provider to Google Gemini
**Prompt:**
Switch the AI provider to Google Gemini through its OpenAI-compatible
endpoint, with model gemini-2.5-flash. Rename AiSettings:GitHubToken to
AiSettings:ApiKey. Keep the system prompt, validation, error handling and
fallback stub unchanged. Update decision D10 in CLAUDE.md and note that
GitHub Models was retired on 30 July 2026. Then run the three live requests
again.

Follow-up prompt:
Set the default model to gemini-3.5-flash. Raise AiSettings:TimeoutSeconds
to 30. Retry once on a 503 from the provider, after a short delay. Do not
retry on timeouts or other errors.

**Result:** Provider switched by configuration only, thanks to the
provider-neutral design. gemini-2.5-flash was not available to new keys.
Newer models were overloaded or slow during testing.

**My review/changes:**
- Chose the assignment's "any free AI provider" option after confirming the
  GitHub Models retirement in GitHub's changelog.
- Chose gemini-3.5-flash over the newest responding model, prioritising
  reliability over recency.
- Limited the retry to provider 503 errors only, so a timeout never doubles
  the user's wait.

---

### Step 7.3 — Fix Truncated AI Answers
**Prompt:**
Add AiSettings:ReasoningEffort (default "none"; empty means do not send the
parameter) and pass it as ReasoningEffortLevel. Raise
AiSettings:MaxOutputTokens to 1500. First make one direct call to check that
gemini-3.5-flash accepts "none". Record the setting and the reason in D10.
Rerun the tests and the three live requests, and write docs/ai-examples.md
from the real outputs.

**Result:** Complete answers for all three live requests. The prompt
injection attempt was ignored. docs/ai-examples.md written from the saved
API responses.

**My review/changes:**
- Root cause found by Claude Code: about 574 of the 600 output tokens were
  spent on hidden reasoning, so answers were cut off. The output validation
  correctly rejected them with 502.
- Chose to disable reasoning, since rewriting a description does not need it,
  and kept the setting configurable for providers that do not support it.
- Noted a limitation for PROMPTS.md: for very short input the model adds
  generic steps that the input did not state.
- Response times of 20–28 seconds were too slow for a good user experience,
  so I asked for a comparison with the lite model (see step 7.4).

---

### Step 7.4 — Faster Model
**Prompt:**
Run the same three live requests on gemini-3.5-flash-lite with the same
settings. If the quality is comparable and responses come back in under about
10 seconds, make it the default model, update D10 in CLAUDE.md and
docs/ai-examples.md, and commit. Otherwise keep gemini-3.5-flash, raise
AiSettings:TimeoutSeconds to 45, record the reason in D10, and commit.

**Result:** gemini-3.5-flash-lite answered in 1.5–2.9 seconds instead of
20–28 seconds, with the same output format and comparable quality. The prompt
injection attempt was ignored again. flash-lite rejects reasoning effort
"none", so the default is "minimal", which also uses no hidden reasoning.

**My review/changes:**
- Defined measurable acceptance criteria (comparable quality, under about 10
  seconds) before switching, with a fallback plan if they were not met.
- Compared the outputs side by side: grammar fixed, separate concerns kept as
  separate steps, and no prompt leak.
- Chose the faster model because a response time of 20+ seconds is too slow
  for a button in the task form.


  ---

### Step 7.5 — Rate Limiting, Health Checks and API Docs
**Prompt:**
Add rate limiting as decided in D15 (global per IP, stricter on auth
endpoints, and 10 per minute per user on the AI endpoint) with 429 responses
in the ApiResponse format. Add health checks including the database; the
/health endpoint must allow anonymous access. Review every endpoint for
missing authorization or validation. Generate docs/database.sql with an
idempotent migration script, and export the swagger.json and a Postman
collection to docs/.

**Result:** Three rate limiting policies (global per IP, auth per IP, AI per
user) returning 429 with Retry-After. Anonymous /health endpoint exempt from
rate limiting, returning 503 when the database is down. Endpoint review of all
25 actions, which found and closed one gap: request bodies are now capped at
1 MB. Idempotent SQL script, OpenAPI document and a Postman collection that
stores the access token and created ids automatically.

**My review/changes:**
- Added the requirement that /health is anonymous, since the default policy
  requires authentication on every endpoint.
- Agreed that refresh and logout use the global limit instead of the strict
  auth limit, so reloading the page cannot lock a user out of their session.
- Verified the AI limit is per user rather than per IP, and that the SQL
  script runs twice on an empty database without errors.
- Noted for the README that deployments behind a proxy need ForwardedHeaders,
  otherwise all clients share one rate limit budget.

  ---

### Step 7.6 — Backend Audit
**Prompt:**
Do a full backend audit before we start the frontend. Do not change any code
yet; report findings first.

1. Requirements: read docs/assignment.pdf and check every backend requirement
   against the actual code. Write docs/REQUIREMENTS_CHECKLIST.md as a table:
   requirement, status, and where it is implemented (file or endpoint). Flag
   anything missing or partial. I already know project lists have no filters.

2. Code quality: review the whole backend for SOLID violations, dead code,
   TODOs, magic strings, inconsistent naming, missing CancellationToken,
   sync-over-async, business logic in controllers, and anything a senior
   reviewer would criticise. List findings by severity.

3. Design patterns: list each pattern actually used, with the file where it
   lives, for the README.

4. Fresh setup: from a clean clone, list every manual step a reviewer needs
   before the API runs. Propose a single tools/setup-dev.ps1 that sets all
   user-secrets with generated values (JWT key, admin password) and leaves the
   AI key optional, since the fallback stub works without it.

5. Test scripts: tell me whether your PowerShell API check scripts can be
   cleaned up and committed to tools/api-checks/ with a README, and run the
   full regression one more time.

**Result:** All backend requirements met except project list filtering.
No high-severity findings; three medium and several low findings. 20 design
patterns identified with their locations. 393 API checks passed.

**My review/changes:**
- Ran the audit as a report first, so I could decide what to fix with the
  deadline in mind.
- Prioritised the missing filter and a one-command setup script, so reviewers
  can run the project easily.
- Chose to fix all three medium findings: immediate effect of role changes
  and deactivation (security), HTTP codes in the Application layer
  (architecture), and the hidden delete cascade (visibility of business rules).
- Kept the register endpoint without a Location header, since there is no
  user details endpoint.
- Decided to commit the API check scripts as evidence of testing, with an
  isolated database so a run never touches real data.

  ---

### Step 7.7 — Audit Fixes
**Prompt:**
Proceed in this order, with a separate commit for each item:

1. Commit docs/REQUIREMENTS_CHECKLIST.md.
2. Project filters: add ownerId, ownedByMe (bool), and createdFrom/createdTo
   to GET /api/projects, using the existing query and validator patterns.
3. Medium 1: make role changes and deactivation take effect immediately.
   On each authenticated request, check the user's current IsActive and role
   against the database. If the user is inactive or the role differs from the
   token, reject with 401 so the client refreshes and gets a token with the
   current role. Keep it to one indexed query. Update D6 in CLAUDE.md.
4. Medium 2: replace the HTTP status codes in ExternalServiceException with a
   failure kind and map it to HTTP in the API layer.
5. Medium 3: move the project-to-task delete cascade into
   ProjectService.DeleteAsync so the rule is visible there.
6. Low findings: dead code, shared constants, options classes for
   configuration, consistent current-user handling and file layout, and
   reject integer enum values.
7. tools/setup-dev.ps1, tested from a fresh clone.
8. API check scripts cleaned up and committed to tools/api-checks/.

After step 6 and after step 8, run the full regression.

**Result:** All eight steps committed separately. Project list filters
(owner, ownedByMe, created date range). Role changes and deactivation now
take effect on the next request through a single primary-key lookup.
Application layer free of HTTP status codes. Delete cascade visible in
ProjectService. Low findings fixed, including a hidden binding issue where
comma-separated enum values were silently read as one value. One-command
setup script and 397 API checks committed in tools/api-checks/, running
against a throwaway database.

**My review/changes:**
- Required the setup script to be tested from a fresh clone; this found a
  real bug (dotnet ef failing before package restore) that would have stopped
  a reviewer from running the project.
- Accepted one extra primary-key query per request as the cost of immediate
  role and deactivation changes.
- Required the API checks to use a throwaway database, so running them can
  never affect real data.
- Verified both regression runs and that no test data was left behind.