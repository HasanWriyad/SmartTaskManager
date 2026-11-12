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