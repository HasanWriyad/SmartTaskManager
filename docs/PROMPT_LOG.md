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

  