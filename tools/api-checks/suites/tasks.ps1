# End-to-end test of /api/projects/{id}/tasks against a running API.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'tasks'


# --- Setup
$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
$adminEmail = ($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', ''
$adminPw = ($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', ''
$A = Login $adminEmail $adminPw
$sfx = "t$(Get-Random -Maximum 99999)"; $pw = 'Str0ng!Passw0rd'
$ids = @{}; $tok = @{}
foreach ($nm in 'pm1', 'pm2', 'tm1', 'tm2', 'tm3', 'out') {
  $ids[$nm] = (Req POST /api/auth/register @{ email = "$nm.$sfx@example.com"; fullName = "Test $nm $sfx"; password = $pw }).Body.data.id
}
foreach ($nm in 'pm1', 'pm2') { $null = Req PUT "/api/users/$($ids[$nm])/role" @{ role = 'ProjectManager' } $A }
foreach ($nm in 'pm1', 'pm2', 'tm1', 'tm2', 'out') { $tok[$nm] = Login "$nm.$sfx@example.com" $pw }
$P = (Req POST /api/projects @{ name = "Tasks $sfx" } $tok.pm1).Body.data.id
$Q = (Req POST /api/projects @{ name = "Other $sfx" } $tok.pm1).Body.data.id
foreach ($nm in 'pm2', 'tm1', 'tm2', 'tm3') { $null = Req POST "/api/projects/$P/members" @{ userId = $ids[$nm] } $tok.pm1 }
$null = Req PUT "/api/users/$($ids.tm3)/status" @{ isActive = $false } $A  # inactive member
Check 'setup: users, project P (members pm2, tm1, tm2, tm3[inactive]), project Q' ([bool]$P -and [bool]$Q -and @($tok.Values | Where-Object { $_ }).Count -eq 5)
$today = [DateTime]::UtcNow.Date
$d = { param($days) $today.AddDays($days).ToString('yyyy-MM-dd') }

# --- Create
$r = Req POST "/api/projects/$P/tasks" @{ title = '  Write spec  '; description = 'Draft'; dueDate = (& $d 7); assigneeId = $ids.tm1 } $tok.pm1
$T1 = $r.Body.data.id
Check 'create: owner -> 201, ToDo, default priority Medium, trimmed, assigned' ($r.Code -eq 201 -and $r.Body.data.status -eq 'ToDo' -and $r.Body.data.priority -eq 'Medium' -and $r.Body.data.title -eq 'Write spec' -and $r.Body.data.assigneeId -eq $ids.tm1 -and $r.Body.data.createdById -eq $ids.pm1) "$($r.Body.data.status)/$($r.Body.data.priority)/'$($r.Body.data.title)'"
Check 'create: details carry names and flags' ($r.Body.data.projectName -eq "Tasks $sfx" -and $r.Body.data.assigneeName -eq "Test tm1 $sfx" -and $r.Body.data.canManage -and $r.Body.data.canUpdateStatus)
$T2 = (Req POST "/api/projects/$P/tasks" @{ title = 'Unassigned critical'; priority = 'Critical' } $tok.pm1).Body.data.id
$T3 = (Req POST "/api/projects/$P/tasks" @{ title = 'Low later'; priority = 'Low'; dueDate = (& $d 30) } $A).Body.data.id
Check 'create: Admin -> 201 in a PM''s project' ([bool]$T3)
Check 'create: member PM (not owner) -> 403' ((Req POST "/api/projects/$P/tasks" @{ title = 'x' } $tok.pm2).Code -eq 403)
Check 'create: member TM -> 403' ((Req POST "/api/projects/$P/tasks" @{ title = 'x' } $tok.tm1).Code -eq 403)
Check 'create: non-member -> 403 (policy) / unknown project -> 404' ((Req POST "/api/projects/$P/tasks" @{ title = 'x' } $tok.out).Code -eq 403 -and (Req POST "/api/projects/$([guid]::NewGuid())/tasks" @{ title = 'x' } $tok.pm1).Code -eq 404)
$r = Req POST "/api/projects/$P/tasks" @{ title = ''; description = ('d' * 4001) } $tok.pm1
Check 'create: blank title + 4001-char description -> 400 both fields' ($r.Code -eq 400 -and $r.Body.errors.title -and $r.Body.errors.description) ("keys: " + ($r.Body.errors.PSObject.Properties.Name -join ','))
$r = Req POST "/api/projects/$P/tasks" @{ title = 'x'; priority = 'Urgent' } $tok.pm1
Check 'create: unknown priority -> 400 keyed "priority" (not "$.priority")' ($r.Code -eq 400 -and ($r.Body.errors.PSObject.Properties.Name -join ',') -eq 'priority') ("keys: " + ($r.Body.errors.PSObject.Properties.Name -join ','))
$r = Req POST "/api/projects/$P/tasks" @{ title = 'Past'; dueDate = (& $d -3) } $tok.pm1
Check 'create: due date 3 days ago -> 400 dueDate' ($r.Code -eq 400 -and $r.Body.errors.dueDate) ($r.Body.errors.dueDate -join '')
Check 'create: due date yesterday (time-zone slack) -> 201' ((Req POST "/api/projects/$P/tasks" @{ title = 'Yesterday ok'; dueDate = (& $d -1) } $tok.pm1).Code -eq 201)
Check 'create: title 201 chars -> 400' ((Req POST "/api/projects/$P/tasks" @{ title = ('t' * 201) } $tok.pm1).Code -eq 400)
foreach ($c in @(@('out', 'non-member'), @('tm3', 'inactive member'), @('new', 'unknown user'))) {
  $aid = if ($c[0] -eq 'new') { [guid]::NewGuid() } else { $ids[$c[0]] }
  $r = Req POST "/api/projects/$P/tasks" @{ title = 'x'; assigneeId = $aid } $tok.pm1
  Check "create: assignee = $($c[1]) -> 400 assigneeId" ($r.Code -eq 400 -and $r.Body.errors.assigneeId) ($r.Body.errors.assigneeId -join '')
}
$r = Req POST "/api/projects/$P/tasks" @{ title = 'Owner self-assigned'; assigneeId = $ids.pm1 } $tok.pm1
$TOwn = $r.Body.data.id
Check 'create: owner (auto-member) as assignee -> 201' ($r.Code -eq 201 -and $r.Body.data.assigneeId -eq $ids.pm1)

# --- Read
foreach ($c in @(@('pm1', 200), @('pm2', 200), @('tm1', 200), @('tm2', 200), @('out', 404))) {
  Check "get: $($c[0]) -> $($c[1])" ((Req GET "/api/projects/$P/tasks/$T1" -bearer $tok[$c[0]]).Code -eq $c[1])
}
$g = (Req GET "/api/projects/$P/tasks/$T1" -bearer $tok.tm1).Body.data
$g2 = (Req GET "/api/projects/$P/tasks/$T1" -bearer $tok.tm2).Body.data
$g3 = (Req GET "/api/projects/$P/tasks/$T1" -bearer $tok.pm2).Body.data
Check 'flags: assignee TM canUpdateStatus only; other TM / member PM neither' (-not $g.canManage -and $g.canUpdateStatus -and -not $g2.canUpdateStatus -and -not $g3.canManage -and -not $g3.canUpdateStatus)
Check 'get: task via wrong project id -> 404' ((Req GET "/api/projects/$Q/tasks/$T1" -bearer $tok.pm1).Code -eq 404)
Check 'get: unknown task -> 404' ((Req GET "/api/projects/$P/tasks/$([guid]::NewGuid())" -bearer $tok.pm1).Code -eq 404)
$list = (Req GET "/api/projects/$P/tasks" -bearer $tok.tm2).Body.data.items
Check 'list: member sees all project tasks, Critical first, no-due-date ordering' (@($list).Count -eq 5 -and $list[0].id -eq $T2) (($list | ForEach-Object { "$($_.priority):$($_.title)" }) -join ' | ')
Check 'list: non-member -> 404' ((Req GET "/api/projects/$P/tasks" -bearer $tok.out).Code -eq 404)

# --- Row flags (canManage / canUpdateStatus on each list row, same rules as the details)
function RowFlags($items, $taskId) { $row = @($items | Where-Object { $_.id -eq $taskId })[0]; if ($row) { "$($row.canManage)/$($row.canUpdateStatus)" } else { 'missing' } }
$rowsBy = @{}
foreach ($who in 'pm1', 'pm2', 'tm1', 'tm2') { $rowsBy[$who] = (Req GET "/api/projects/$P/tasks?pageSize=100" -bearer $tok[$who]).Body.data.items }
$rowsBy['admin'] = (Req GET "/api/projects/$P/tasks?pageSize=100" -bearer $A).Body.data.items
Check 'row flags: owner PM and Admin manage every row' (@($rowsBy.pm1 + $rowsBy.admin | Where-Object { -not ($_.canManage -and $_.canUpdateStatus) }).Count -eq 0)
Check 'row flags: assignee TM -> False/True on own task, False/False on others' ((RowFlags $rowsBy.tm1 $T1) -eq 'False/True' -and (RowFlags $rowsBy.tm1 $T2) -eq 'False/False') "$(RowFlags $rowsBy.tm1 $T1), $(RowFlags $rowsBy.tm1 $T2)"
Check 'row flags: other TM and member PM -> False/False' ((RowFlags $rowsBy.tm2 $T1) -eq 'False/False' -and @($rowsBy.pm2 | Where-Object { $_.canManage -or $_.canUpdateStatus }).Count -eq 0)
$all = (Req GET "/api/tasks?projectId=$P&pageSize=100" -bearer $tok.tm1).Body.data.items
Check 'row flags: GET /api/tasks carries the same flags' ((RowFlags $all $T1) -eq 'False/True' -and (RowFlags $all $T2) -eq 'False/False') "$(RowFlags $all $T1), $(RowFlags $all $T2)"
$all = (Req GET "/api/tasks?projectId=$P&pageSize=100" -bearer $tok.pm1).Body.data.items
Check 'row flags: GET /api/tasks owner -> True/True' ((RowFlags $all $T1) -eq 'True/True' -and (RowFlags $all $T2) -eq 'True/True')

# --- Update
$r = Req PUT "/api/projects/$P/tasks/$T1" @{ title = 'Write spec v2'; description = $null; priority = 'High'; dueDate = (& $d 10) } $tok.pm1
Check 'update: owner -> 200, fields changed, description cleared, updatedAt' ($r.Code -eq 200 -and $r.Body.data.title -eq 'Write spec v2' -and $r.Body.data.priority -eq 'High' -and $null -eq $r.Body.data.description -and $r.Body.data.updatedAt -and $r.Body.data.assigneeId -eq $ids.tm1)
Check 'update: missing priority -> 400 (no silent reset)' ((Req PUT "/api/projects/$P/tasks/$T1" @{ title = 'x' } $tok.pm1).Code -eq 400)
Check 'update: new past due date -> 400' ((Req PUT "/api/projects/$P/tasks/$T1" @{ title = 'x'; priority = 'High'; dueDate = (& $d -5) } $tok.pm1).Code -eq 400)
Sql "UPDATE Tasks SET DueDate = '$(& $d -10)' WHERE Id = '$T3'" | Out-Null
$r = Req PUT "/api/projects/$P/tasks/$T3" @{ title = 'Low later (renamed)'; priority = 'Low'; dueDate = (& $d -10) } $tok.pm1
Check 'update: keeping an existing overdue date -> 200' ($r.Code -eq 200 -and $r.Body.data.dueDate -eq (& $d -10))
Check 'update: clearing due date (null) -> 200' ((Req PUT "/api/projects/$P/tasks/$T3" @{ title = 'Low later'; priority = 'Low'; dueDate = $null } $tok.pm1).Body.data.dueDate -eq $null)
Check 'update: member PM -> 403, TM assignee -> 403' ((Req PUT "/api/projects/$P/tasks/$T1" @{ title = 'x'; priority = 'Low' } $tok.pm2).Code -eq 403 -and (Req PUT "/api/projects/$P/tasks/$T1" @{ title = 'x'; priority = 'Low' } $tok.tm1).Code -eq 403)
Check 'update: Admin -> 200' ((Req PUT "/api/projects/$P/tasks/$T1" @{ title = 'Write spec v2'; priority = 'High'; dueDate = (& $d 10) } $A).Code -eq 200)

# --- Assign
$r = Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.tm2 } $tok.pm1
Check 'assign: owner assigns tm2 -> 200' ($r.Code -eq 200 -and $r.Body.data.assigneeId -eq $ids.tm2)
Check 'assign: reassign to pm2 (member PM) -> 200' ((Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.pm2 } $tok.pm1).Body.data.assigneeId -eq $ids.pm2)
Check 'assign: same assignee again -> 200 no-op' ((Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.pm2 } $tok.pm1).Code -eq 200)
Check 'assign: body {} -> 400 (does not unassign)' ((Req PUT "/api/projects/$P/tasks/$T2/assignee" '{}' $tok.pm1).Code -eq 400 -and (Req GET "/api/projects/$P/tasks/$T2" -bearer $tok.pm1).Body.data.assigneeId -eq $ids.pm2)
Check 'assign: non-member / inactive -> 400' ((Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.out } $tok.pm1).Code -eq 400 -and (Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.tm3 } $tok.pm1).Code -eq 400)
Check 'assign: TM -> 403, member PM -> 403' ((Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.tm1 } $tok.tm1).Code -eq 403 -and (Req PUT "/api/projects/$P/tasks/$T2/assignee" @{ assigneeId = $ids.tm1 } $tok.pm2).Code -eq 403)
$r = Req DELETE "/api/projects/$P/tasks/$T2/assignee" -bearer $tok.pm1
Check 'unassign: owner -> 200, assignee null' ($r.Code -eq 200 -and $null -eq $r.Body.data.assigneeId -and $null -eq $r.Body.data.assigneeName)
Check 'unassign: again -> 200 no-op; TM -> 403' ((Req DELETE "/api/projects/$P/tasks/$T2/assignee" -bearer $tok.pm1).Code -eq 200 -and (Req DELETE "/api/projects/$P/tasks/$T1/assignee" -bearer $tok.tm1).Code -eq 403)

# --- Status
function St($task, $status, $who) { Req PUT "/api/projects/$P/tasks/$task/status" @{ status = $status } $tok[$who] }
$r = St $T1 'InProgress' 'tm1'
Check 'status: TM assignee ToDo -> InProgress -> 200' ($r.Code -eq 200 -and $r.Body.data.status -eq 'InProgress' -and $null -eq $r.Body.data.completedAt)
$r = St $T1 'Completed' 'tm1'
$completedAt = $r.Body.data.completedAt
Check 'status: TM assignee -> Completed sets completedAt' ($r.Code -eq 200 -and $r.Body.data.status -eq 'Completed' -and $completedAt) "$completedAt"
$r = St $T1 'Completed' 'tm1'
Check 'status: Completed again -> 200, completedAt unchanged' ($r.Code -eq 200 -and $r.Body.data.completedAt -eq $completedAt)
$r = St $T1 'InProgress' 'tm1'
Check 'status: reopen Completed -> InProgress clears completedAt' ($r.Code -eq 200 -and $null -eq $r.Body.data.completedAt)
Check 'status: TM non-assignee -> 403' ((St $T1 'ToDo' 'tm2').Code -eq 403)
Check 'status: member PM (not owner) -> 403' ((St $T1 'ToDo' 'pm2').Code -eq 403)
Check 'status: non-member -> 404' ((St $T1 'ToDo' 'out').Code -eq 404)
Check 'status: TM on unassigned task -> 403' ((St $T2 'InProgress' 'tm1').Code -eq 403)
$r = St $T1 'Cancelled' 'tm1'
Check 'status: TM assignee cancels -> 200' ($r.Code -eq 200 -and $r.Body.data.status -eq 'Cancelled')
$r = St $T1 'InProgress' 'pm1'
Check 'status: Cancelled -> InProgress (owner) -> 422' ($r.Code -eq 422) $r.Body.message
Check 'status: Cancelled -> Completed (Admin) -> 422' ((Req PUT "/api/projects/$P/tasks/$T1/status" @{ status = 'Completed' } $A).Code -eq 422)
$r = St $T1 'ToDo' 'tm1'
Check 'status: TM assignee reopens Cancelled -> ToDo -> 403' ($r.Code -eq 403) $r.Body.message
$r = St $T1 'ToDo' 'pm1'
Check 'status: owner reopens Cancelled -> ToDo -> 200' ($r.Code -eq 200 -and $r.Body.data.status -eq 'ToDo' -and $null -eq $r.Body.data.completedAt)
foreach ($b in '{}', '{"status":"Done"}', '{"status":42}', '{"status":2}') {
  Check "status: body $b -> 400" ((Req PUT "/api/projects/$P/tasks/$T1/status" $b $tok.pm1).Code -eq 400)
}
# Any active assignee, whatever their role, may move their own task (but not reopen a cancelled one).
$TPm = (Req POST "/api/projects/$P/tasks" @{ title = 'For member PM'; assigneeId = $ids.pm2 } $tok.pm1).Body.data.id
$g = (Req GET "/api/projects/$P/tasks/$TPm" -bearer $tok.pm2).Body.data
Check 'assignee PM (member, not owner): canUpdateStatus true, canManage false' ($g.canUpdateStatus -and -not $g.canManage)
$row = @((Req GET "/api/tasks?projectId=$P&pageSize=100" -bearer $tok.pm2).Body.data.items | Where-Object { $_.id -eq $TPm })[0]
Check 'row flags: assignee PM (not owner) -> canManage false, canUpdateStatus true' ($row -and -not $row.canManage -and $row.canUpdateStatus)
# Every row of every viewer matches that task's details flags.
$mismatch = @()
foreach ($who in 'pm1', 'pm2', 'tm1', 'tm2', 'admin') {
  $bearer = if ($who -eq 'admin') { $A } else { $tok[$who] }
  foreach ($item in (Req GET "/api/projects/$P/tasks?pageSize=100" -bearer $bearer).Body.data.items) {
    $det = (Req GET "/api/projects/$P/tasks/$($item.id)" -bearer $bearer).Body.data
    if ($det.canManage -ne $item.canManage -or $det.canUpdateStatus -ne $item.canUpdateStatus) { $mismatch += "$who/$($item.title)" }
  }
}
Check 'row flags equal the details flags for every viewer and task' ($mismatch.Count -eq 0) ($mismatch -join ', ')
Check 'status: assignee PM (not owner) InProgress -> 200' ((St $TPm 'InProgress' 'pm2').Body.data.status -eq 'InProgress')
Check 'status: assignee PM cancels -> 200' ((St $TPm 'Cancelled' 'pm2').Body.data.status -eq 'Cancelled')
Check 'status: assignee PM reopens Cancelled -> 403' ((St $TPm 'ToDo' 'pm2').Code -eq 403)
Check 'status: assignee PM still cannot edit/assign/delete -> 403' ((Req PUT "/api/projects/$P/tasks/$TPm" @{ title = 'x'; priority = 'Low' } $tok.pm2).Code -eq 403 -and (Req DELETE "/api/projects/$P/tasks/$TPm" -bearer $tok.pm2).Code -eq 403)
$TTm2 = (Req POST "/api/projects/$P/tasks" @{ title = 'For tm2'; assigneeId = $ids.tm2 } $tok.pm1).Body.data.id
Check 'status: assignee tm2 (active) -> 200' ((St $TTm2 'InProgress' 'tm2').Code -eq 200)
$null = Req PUT "/api/users/$($ids.tm2)/status" @{ isActive = $false } $A
$r = St $TTm2 'Completed' 'tm2'
Check 'status: deactivated assignee with still-valid token -> 401 (rejected at authentication)' ($r.Code -eq 401 -and (Req GET "/api/projects/$P/tasks/$TTm2" -bearer $tok.pm1).Body.data.status -eq 'InProgress') $r.Body.message
Check 'status: owner on own assigned task -> 200' ((St $TOwn 'Completed' 'pm1').Code -eq 200)

$r = Req PUT "/api/projects/$P/tasks/$T2/status" @{ status = 'Completed' } $A
$db = Sql "SELECT CONCAT(Status, ',', IIF(CompletedAt IS NULL, 'null', 'set')) FROM Tasks WHERE Id = '$T2'"
Check 'status: Admin completes directly from ToDo; DB Status=2, CompletedAt set' ($r.Code -eq 200 -and $db -eq '2,set') "$db"

# --- Delete
Check 'delete: TM -> 403, member PM -> 403' ((Req DELETE "/api/projects/$P/tasks/$T3" -bearer $tok.tm1).Code -eq 403 -and (Req DELETE "/api/projects/$P/tasks/$T3" -bearer $tok.pm2).Code -eq 403)
$before = (Req GET "/api/projects/$P" -bearer $tok.pm1).Body.data.taskCount
$r = Req DELETE "/api/projects/$P/tasks/$T3" -bearer $tok.pm1
$row = Sql "SELECT CONCAT(CAST(IsDeleted AS int), ',', IIF(DeletedAt IS NULL, 'null', 'set'), ',', CONVERT(varchar(36), DeletedById), ',', Title) FROM Tasks WHERE Id = '$T3'"
Check 'delete: owner -> 200; row kept IsDeleted=1, DeletedById=owner' ($r.Code -eq 200 -and $row -eq "1,set,$($ids.pm1),Low later") "$row"
Check 'delete: get -> 404, not listed, project taskCount -1' ((Req GET "/api/projects/$P/tasks/$T3" -bearer $tok.pm1).Code -eq 404 -and (Req GET "/api/projects/$P/tasks" -bearer $tok.pm1).Body.data.items.id -notcontains $T3 -and (Req GET "/api/projects/$P" -bearer $tok.pm1).Body.data.taskCount -eq ($before - 1))
Check 'delete: again / update / status -> 404' ((Req DELETE "/api/projects/$P/tasks/$T3" -bearer $tok.pm1).Code -eq 404 -and (Req PUT "/api/projects/$P/tasks/$T3/status" @{ status = 'ToDo' } $tok.pm1).Code -eq 404)
$null = Req DELETE "/api/projects/$P" -bearer $tok.pm1
Check 'project deleted -> its tasks 404 for owner and Admin' ((Req GET "/api/projects/$P/tasks/$T1" -bearer $tok.pm1).Code -eq 404 -and (Req GET "/api/projects/$P/tasks" -bearer $A).Code -eq 404)

# --- Cleanup
$idList = ($ids.Values | ForEach-Object { "'$_'" }) -join ','
Sql "DELETE t FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE p.OwnerId IN ($idList); DELETE m FROM ProjectMembers m JOIN Projects p ON p.Id = m.ProjectId WHERE p.OwnerId IN ($idList); DELETE FROM Projects WHERE OwnerId IN ($idList); DELETE FROM RefreshTokens WHERE UserId IN ($idList); DELETE FROM Users WHERE Id IN ($idList)" | Out-Null
Check 'cleanup: test data removed' ((Sql "SELECT COUNT(*) FROM Users WHERE Email LIKE '%.$sfx@example.com'") -eq '0' -and (Sql "SELECT COUNT(*) FROM Projects WHERE Name LIKE N'%$sfx%'") -eq '0')
Complete-Suite
