# GET /api/dashboard: definitions, role scoping, exclusions, and query shape.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'dashboard'

function Summ($d) {
  $s = ($d.tasksByStatus | ForEach-Object { "$($_.status)=$($_.count)" }) -join ' '
  $p = ($d.tasksByPriority | ForEach-Object { "$($_.priority)=$($_.count)" }) -join ' '
  "proj=$($d.totalProjects) tasks=$($d.totalTasks) | $s | $p | done=$($d.completion.completed) pend=$($d.completion.pending) canc=$($d.completion.cancelled) | overdue=$($d.overdueCount) upcoming=$($d.upcomingCount)"
}

# --- Setup
$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
$A = Login (($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', '') (($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', '')
$sfx = "d$(Get-Random -Maximum 99999)"; $pw = 'Str0ng!Passw0rd'
$ids = @{}; $tok = @{}
foreach ($nm in 'pm1', 'pm2', 'tm1', 'tm2') { $ids[$nm] = (Req POST /api/auth/register @{ email = "$nm.$sfx@example.com"; fullName = "Test $nm $sfx"; password = $pw }).Body.data.id }
foreach ($nm in 'pm1', 'pm2') { $null = Req PUT "/api/users/$($ids[$nm])/role" @{ role = 'ProjectManager' } $A }
foreach ($nm in 'pm1', 'pm2', 'tm1', 'tm2') { $tok[$nm] = Login "$nm.$sfx@example.com" $pw }

$P1 = (Req POST /api/projects @{ name = "P1 $sfx" } $tok.pm1).Body.data.id
$P2 = (Req POST /api/projects @{ name = "P2 $sfx" } $tok.pm1).Body.data.id
$P3 = (Req POST /api/projects @{ name = "P3 $sfx" } $tok.pm2).Body.data.id
$P4 = (Req POST /api/projects @{ name = "P4 deleted $sfx" } $tok.pm1).Body.data.id
$null = Req POST "/api/projects/$P1/members" @{ userId = $ids.tm1 } $tok.pm1

$today = [DateTime]::UtcNow.Date
function Day($n) { $today.AddDays($n).ToString('yyyy-MM-dd') }
# [Nullable[int]]: a bare "-2" argument would otherwise bind as the string "-2".
function NewTask($project, $owner, $title, $priority, [Nullable[int]]$due) {
  $b = @{ title = "$title $sfx"; priority = $priority }; if ($null -ne $due -and $due -ge 0) { $b.dueDate = (Day $due) }
  $id = (Req POST "/api/projects/$project/tasks" $b $tok[$owner]).Body.data.id
  if ($null -ne $due -and $due -lt 0) { Sql "UPDATE Tasks SET DueDate = '$(Day $due)' WHERE Id = '$id'" | Out-Null } # past dates can't be set via the API
  $id
}
function SetStatus($project, $id, $status, $owner) { $null = Req PUT "/api/projects/$project/tasks/$id/status" @{ status = $status } $tok[$owner] }

$t = @{}
$t.a = NewTask $P1 pm1 'a overdue' 'High' -2
$t.b = NewTask $P1 pm1 'b due today' 'Critical' 0;  SetStatus $P1 $t.b 'InProgress' pm1
$t.c = NewTask $P1 pm1 'c due +7' 'Medium' 7
$t.d = NewTask $P1 pm1 'd due +8' 'Low' 8
$t.e = NewTask $P1 pm1 'e completed late' 'Medium' -5; SetStatus $P1 $t.e 'Completed' pm1
$t.f = NewTask $P1 pm1 'f cancelled soon' 'High' 1;   SetStatus $P1 $t.f 'Cancelled' pm1
$t.g = NewTask $P1 pm1 'g no due' 'Medium' $null
function AssignTo($project, $id, $user, $owner) { $null = Req PUT "/api/projects/$project/tasks/$id/assignee" @{ assigneeId = $ids[$user] } $tok[$owner] }
$t.h = NewTask $P1 pm1 'h deleted' 'Critical' 1;      AssignTo $P1 $t.h tm1 pm1; $null = Req DELETE "/api/projects/$P1/tasks/$($t.h)" -bearer $tok.pm1
$t.i = NewTask $P2 pm1 'i P2 +3' 'Low' 3;             SetStatus $P2 $t.i 'InProgress' pm1
$t.j = NewTask $P3 pm2 'j P3 overdue' 'Critical' -1
$t.k = NewTask $P4 pm1 'k deleted project' 'High' 2;  $null = Req DELETE "/api/projects/$P4" -bearer $tok.pm1
Check 'setup: 11 tasks created (h, k and P4 then soft-deleted)' (@($t.Values | Where-Object { $_ }).Count -eq 11)
# My tasks: tm1 <- a (overdue ToDo), g (ToDo, no due), e (Completed), h (deleted); pm1 <- b, i (InProgress); pm2 <- j (overdue)
AssignTo $P1 $t.a tm1 pm1; AssignTo $P1 $t.g tm1 pm1; AssignTo $P1 $t.e tm1 pm1
AssignTo $P1 $t.b pm1 pm1; AssignTo $P2 $t.i pm1 pm1; AssignTo $P3 $t.j pm2 pm2
$assigned = Sql "SELECT COUNT(*) FROM Tasks WHERE AssigneeId IS NOT NULL AND ProjectId IN ('$P1','$P2','$P3')"
Check 'setup: 7 assignments in place (incl. the deleted h)' ($assigned -eq '7') "$assigned"

# --- ProjectManager pm1: owns P1, P2 (P4 deleted); not a member of P3
$r = Req GET /api/dashboard -bearer $tok.pm1
$d = $r.Body.data
"       pm1: $(Summ $d)"
Check 'pm1: 200, asOf = UTC today, window 7' ($r.Code -eq 200 -and $d.asOf -eq (Day 0) -and $d.upcomingWindowDays -eq 7)
Check 'pm1: 2 projects, 8 tasks (deleted task/project excluded)' ($d.totalProjects -eq 2 -and $d.totalTasks -eq 8)
Check 'pm1: by status ToDo 4, InProgress 2, Completed 1, Cancelled 1' ((($d.tasksByStatus | ForEach-Object { "$($_.status)=$($_.count)" }) -join ',') -eq 'ToDo=4,InProgress=2,Completed=1,Cancelled=1')
Check 'pm1: by priority Low 2, Medium 3, High 2, Critical 1' ((($d.tasksByPriority | ForEach-Object { "$($_.priority)=$($_.count)" }) -join ',') -eq 'Low=2,Medium=3,High=2,Critical=1')
Check 'pm1: completed 1, pending 6 (ToDo+InProgress), cancelled 1' ($d.completion.completed -eq 1 -and $d.completion.pending -eq 6 -and $d.completion.cancelled -eq 1)
Check 'pm1: overdue 1 (a; completed e not counted)' ($d.overdueCount -eq 1)
Check 'pm1: upcoming 3 (today, +3, +7 inclusive; +8 and cancelled excluded)' ($d.upcomingCount -eq 3)
$ord = ($d.upcomingTasks | ForEach-Object { ($_.title -split ' ')[0] }) -join ''
Check 'pm1: upcoming list soonest first: b, i, c' ($ord -eq 'bic') $ord
Check 'pm1: upcoming items carry project name, due date, assignee' ($d.upcomingTasks[0].projectName -eq "P1 $sfx" -and $d.upcomingTasks[0].dueDate -eq (Day 0) -and $d.upcomingTasks[1].projectName -eq "P2 $sfx" -and $d.upcomingTasks[0].assigneeName -eq "Test pm1 $sfx")
Check 'pm1: my tasks pending 2 (b, i across P1/P2), overdue 0' ($d.myTasks.pending -eq 2 -and $d.myTasks.overdue -eq 0) "pending=$($d.myTasks.pending) overdue=$($d.myTasks.overdue)"

# --- TeamMember tm1: member of P1 only
$d = (Req GET /api/dashboard -bearer $tok.tm1).Body.data
"       tm1: $(Summ $d)"
Check 'tm1: 1 project, 7 tasks (P1 only)' ($d.totalProjects -eq 1 -and $d.totalTasks -eq 7)
Check 'tm1: my tasks pending 2 (a, g; completed e and deleted h excluded), overdue 1 (a)' ($d.myTasks.pending -eq 2 -and $d.myTasks.overdue -eq 1) "pending=$($d.myTasks.pending) overdue=$($d.myTasks.overdue)"
Check 'tm1: pending 5, overdue 1, upcoming 2 (b, c)' ($d.completion.pending -eq 5 -and $d.overdueCount -eq 1 -and $d.upcomingCount -eq 2 -and (($d.upcomingTasks | ForEach-Object { ($_.title -split ' ')[0] }) -join '') -eq 'bc')

# --- ProjectManager pm2: owns P3 only
$d = (Req GET /api/dashboard -bearer $tok.pm2).Body.data
Check 'pm2: 1 project, 1 task, overdue 1, upcoming 0' ($d.totalProjects -eq 1 -and $d.totalTasks -eq 1 -and $d.overdueCount -eq 1 -and $d.upcomingCount -eq 0 -and @($d.upcomingTasks).Count -eq 0)
Check 'pm2: my tasks pending 1, overdue 1 (j)' ($d.myTasks.pending -eq 1 -and $d.myTasks.overdue -eq 1)

# --- No projects: zeros, but every status/priority still listed
$d = (Req GET /api/dashboard -bearer $tok.tm2).Body.data
Check 'tm2 (no projects): all zeros, 4 status + 4 priority entries' ($d.totalProjects -eq 0 -and $d.totalTasks -eq 0 -and @($d.tasksByStatus).Count -eq 4 -and @($d.tasksByPriority).Count -eq 4 -and -not ($d.tasksByStatus | Where-Object { $_.count -ne 0 }) -and $d.completion.pending -eq 0 -and $d.myTasks.pending -eq 0 -and $d.myTasks.overdue -eq 0)

# --- Admin: global numbers must match an independent SQL computation over the whole database
$today0 = Day 0; $until = Day 7
$exp = Sql @"
SELECT CONCAT(
  (SELECT COUNT(*) FROM Projects WHERE IsDeleted = 0), ',',
  (SELECT COUNT(*) FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE t.IsDeleted = 0 AND p.IsDeleted = 0), ',',
  (SELECT COUNT(*) FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE t.IsDeleted = 0 AND p.IsDeleted = 0 AND t.Status IN (0,1)), ',',
  (SELECT COUNT(*) FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE t.IsDeleted = 0 AND p.IsDeleted = 0 AND t.Status = 2), ',',
  (SELECT COUNT(*) FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE t.IsDeleted = 0 AND p.IsDeleted = 0 AND t.Status IN (0,1) AND t.DueDate < '$today0'), ',',
  (SELECT COUNT(*) FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE t.IsDeleted = 0 AND p.IsDeleted = 0 AND t.Status IN (0,1) AND t.DueDate BETWEEN '$today0' AND '$until'))
"@
$d = (Req GET /api/dashboard -bearer $A).Body.data
$got = "$($d.totalProjects),$($d.totalTasks),$($d.completion.pending),$($d.completion.completed),$($d.overdueCount),$($d.upcomingCount)"
Check 'admin: global numbers match independent SQL (proj,tasks,pend,done,overdue,upcoming)' ($got -eq $exp) "api=$got sql=$exp"
$adminId = (Req GET "/api/users?search=$((($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', ''))" -bearer $A).Body.data.items[0].id
$expMine = Sql "SELECT CONCAT(COUNT(*), ',', SUM(CASE WHEN t.DueDate < '$today0' THEN 1 ELSE 0 END)) FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE t.IsDeleted = 0 AND p.IsDeleted = 0 AND t.Status IN (0,1) AND t.AssigneeId = '$adminId'"
$expMine = $expMine -replace ',$', ',0'
Check 'admin: my tasks match SQL for tasks assigned to the Admin' ("$($d.myTasks.pending),$($d.myTasks.overdue)" -eq $expMine) "api=$($d.myTasks.pending),$($d.myTasks.overdue) sql=$expMine"
Check 'admin: by-status and by-priority sums equal totalTasks' ((($d.tasksByStatus | Measure-Object count -Sum).Sum -eq $d.totalTasks) -and (($d.tasksByPriority | Measure-Object count -Sum).Sum -eq $d.totalTasks))
Check 'anonymous: 401' ((Req GET /api/dashboard).Code -eq 401)

# --- Query shape: one request = 3 SQL commands, aggregated with GROUP BY, no full entity loads
if ($ServerLog) {
  Start-Sleep -Milliseconds 300
  $mark = @(Get-Content $ServerLog).Count
  $null = Req GET /api/dashboard -bearer $tok.pm1
  Start-Sleep -Milliseconds 500
  $lines = @(Get-Content $ServerLog | Select-Object -Skip $mark)
  $cmds = @($lines | Select-String 'Executed DbCommand').Count
  $text = $lines -join "`n"
  $authCheck = @($lines | Select-String 'SELECT TOP\(2\) \[u\]\.\[IsActive\], \[u\]\.\[Role\]').Count
  Check 'query shape: 1 token-state lookup + 3 dashboard queries' ($cmds -eq 4 -and $authCheck -eq 1) "commands=$cmds tokenCheck=$authCheck"
  Check 'query shape: task counts use GROUP BY with CASE buckets' ($text -match 'GROUP BY' -and $text -match 'CASE')
  # EF computes the CASE keys in an inner SELECT and groups by their aliases.
  $groupLine = ($lines | Select-String 'GROUP BY' | Select-Object -First 1).Line
  Check 'query shape: "assigned to me" is a key of the same GROUP BY' ($groupLine -match '\[AssignedToViewer\]' -and $text -match '\[AssigneeId\] = @') $groupLine.Trim()
  Check 'query shape: upcoming list uses TOP, no Description column read' ($text -match 'SELECT TOP\(' -and $text -notmatch '\[Description\]')
}

# --- Cleanup
$idList = ($ids.Values | ForEach-Object { "'$_'" }) -join ','
Sql "DELETE t FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE p.OwnerId IN ($idList); DELETE m FROM ProjectMembers m JOIN Projects p ON p.Id = m.ProjectId WHERE p.OwnerId IN ($idList); DELETE FROM Projects WHERE OwnerId IN ($idList); DELETE FROM RefreshTokens WHERE UserId IN ($idList); DELETE FROM Users WHERE Id IN ($idList)" | Out-Null
Check 'cleanup: test data removed' ((Sql "SELECT COUNT(*) FROM Users WHERE Email LIKE '%.$sfx@example.com'") -eq '0' -and (Sql "SELECT COUNT(*) FROM Projects WHERE Name LIKE N'%$sfx%'") -eq '0')
Complete-Suite
