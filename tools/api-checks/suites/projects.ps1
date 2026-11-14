# End-to-end test of /api/projects against a running API (https profile), incl. soft delete + cascade.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'projects'


# --- Setup: users and roles
$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
$adminEmail = ($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', ''
$adminPw = ($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', ''
$A = Login $adminEmail $adminPw
$sfx = "p$(Get-Random -Maximum 99999)"; $pw = 'Str0ng!Passw0rd'
$ids = @{}; $tok = @{}
foreach ($nm in 'pm1', 'pm2', 'pm3', 'tm1', 'tm2', 'tm3') {
  $ids[$nm] = (Req POST /api/auth/register @{ email = "$nm.$sfx@example.com"; fullName = "Test $nm $sfx"; password = $pw }).Body.data.id
}
foreach ($nm in 'pm1', 'pm2', 'pm3') { $null = Req PUT "/api/users/$($ids[$nm])/role" @{ role = 'ProjectManager' } $A }
$null = Req PUT "/api/users/$($ids.tm3)/status" @{ isActive = $false } $A
foreach ($nm in 'pm1', 'pm2', 'pm3', 'tm1', 'tm2') { $tok[$nm] = Login "$nm.$sfx@example.com" $pw }
Check 'setup: 6 users, 3 PMs, tm3 inactive, 5 logged in' (@($ids.Values | Where-Object { $_ }).Count -eq 6 -and @($tok.Values | Where-Object { $_ }).Count -eq 5)

# --- Create
$r = Req POST /api/projects @{ name = "  Apollo $sfx  "; description = 'Moon' } $tok.pm1
$P1 = $r.Body.data.id
Check 'create: PM -> 201, owner = caller, name trimmed, canManage' ($r.Code -eq 201 -and $r.Body.data.ownerId -eq $ids.pm1 -and $r.Body.data.name -eq "Apollo $sfx" -and $r.Body.data.canManage) "name='$($r.Body.data.name)'"
Check 'create: owner is the first member' (@($r.Body.data.members).Count -eq 1 -and $r.Body.data.members[0].userId -eq $ids.pm1)
$hb = Join-Path $tmp 'hdr.json'; [IO.File]::WriteAllText($hb, "{`"name`":`"Hdr $sfx`"}")
$h = "$(curl.exe -sk -D - -o NUL -X POST "$Base/api/projects" -H "Authorization: Bearer $($tok.pm1)" -H 'Content-Type: application/json' --data-binary "@$hb" | Select-String '^location:')".Trim()
Check 'create: Location header = /api/projects/{id}' ($h -match '/api/projects/[0-9a-f-]{36}$') $h
$P2 = (Req POST /api/projects @{ name = "Gemini $sfx" } $tok.pm1).Body.data.id
$r = Req POST /api/projects @{ name = "apollo $sfx" } $tok.pm1
Check 'create: duplicate name for same owner (case-insensitive) -> 409' ($r.Code -eq 409) $r.Body.message
Check 'create: same name, different owner -> 201' ((Req POST /api/projects @{ name = "Apollo $sfx" } $tok.pm3).Code -eq 201)
$r = Req POST /api/projects @{ name = 'X' } $tok.tm1
Check 'create: TeamMember -> 403' ($r.Code -eq 403)
Check 'create: Admin -> 201' ((Req POST /api/projects @{ name = "Admin proj $sfx" } $A).Code -eq 201)
$r = Req POST /api/projects @{ name = '   '; description = ('d' * 2001) } $tok.pm1
Check 'create: blank name + 2001-char description -> 400' ($r.Code -eq 400 -and $r.Body.errors.name -and $r.Body.errors.description)
Check 'create: 201-char name -> 400' ((Req POST /api/projects @{ name = ('n' * 201) } $tok.pm1).Code -eq 400)

# --- Members
$r = Req POST "/api/projects/$P1/members" @{ userId = $ids.tm1 } $tok.pm1
Check 'members: owner adds tm1 -> 200, listed' ($r.Code -eq 200 -and $r.Body.data.members.userId -contains $ids.tm1)
$null = Req POST "/api/projects/$P1/members" @{ userId = $ids.pm2 } $tok.pm1
Check 'members: add same user again -> 409' ((Req POST "/api/projects/$P1/members" @{ userId = $ids.tm1 } $tok.pm1).Code -eq 409)
Check 'members: add inactive user -> 409' ((Req POST "/api/projects/$P1/members" @{ userId = $ids.tm3 } $tok.pm1).Code -eq 409)
Check 'members: add unknown user -> 404' ((Req POST "/api/projects/$P1/members" @{ userId = [guid]::NewGuid() } $tok.pm1).Code -eq 404)
Check 'members: missing userId -> 400' ((Req POST "/api/projects/$P1/members" '{}' $tok.pm1).Code -eq 400)
Check 'members: PM2 (member, not owner) adds -> 403' ((Req POST "/api/projects/$P1/members" @{ userId = $ids.tm2 } $tok.pm2).Code -eq 403)
Check 'members: PM3 (not a member) adds -> 404' ((Req POST "/api/projects/$P1/members" @{ userId = $ids.tm2 } $tok.pm3).Code -eq 404)
Check 'members: TM1 (member) adds -> 403 (policy)' ((Req POST "/api/projects/$P1/members" @{ userId = $ids.tm2 } $tok.tm1).Code -eq 403)

# --- Visibility (get + list)
foreach ($c in @(@('pm1', 200, 'owner'), @('pm2', 200, 'member PM'), @('tm1', 200, 'member TM'), @('pm3', 404, 'non-member PM'), @('tm2', 404, 'non-member TM'))) {
  $r = Req GET "/api/projects/$P1" -bearer $tok[$c[0]]
  Check "get: $($c[2]) -> $($c[1])" ($r.Code -eq $c[1]) $(if ($r.Code -eq 404) { $r.Body.message })
}
$r = Req GET "/api/projects/$P1" -bearer $A
Check 'get: Admin -> 200, canManage' ($r.Code -eq 200 -and $r.Body.data.canManage)
Check 'get: canManage false for member PM and member TM' (-not (Req GET "/api/projects/$P1" -bearer $tok.pm2).Body.data.canManage -and -not (Req GET "/api/projects/$P1" -bearer $tok.tm1).Body.data.canManage)
Check 'get: unknown id -> 404 (same as invisible)' ((Req GET "/api/projects/$([guid]::NewGuid())" -bearer $A).Code -eq 404)
$mine = (Req GET '/api/projects?pageSize=100' -bearer $tok.tm1).Body.data.items
Check 'list: TM1 sees only P1 (member); memberCount 3 incl. owner' (@($mine).Count -eq 1 -and $mine[0].id -eq $P1 -and $mine[0].memberCount -eq 3) "memberCount=$($mine[0].memberCount)"
$mine = (Req GET '/api/projects?pageSize=100' -bearer $tok.pm2).Body.data.items
Check 'list: PM2 sees only P1 (member, owns none)' (@($mine).Count -eq 1 -and $mine[0].id -eq $P1)
$mine = (Req GET '/api/projects?pageSize=100' -bearer $tok.pm1).Body.data.items
Check 'list: PM1 sees own P1 + P2 (+ header proj), sorted by name' (($mine.id -contains $P1) -and ($mine.id -contains $P2) -and (($mine.name -join '|') -eq (($mine.name | Sort-Object) -join '|')))
Check 'list: TM2 sees nothing' (@((Req GET '/api/projects?pageSize=100' -bearer $tok.tm2).Body.data.items).Count -eq 0)
$all = (Req GET '/api/projects?pageSize=100' -bearer $A).Body.data.items
Check 'list: Admin sees projects of all owners' (($all.id -contains $P1) -and @($all | Where-Object { $_.ownerId -eq $ids.pm3 }).Count -ge 1)

# --- Update
$r = Req PUT "/api/projects/$P1" @{ name = "Apollo 11 $sfx"; description = 'Updated' } $tok.pm1
Check 'update: owner -> 200, updatedAt set' ($r.Code -eq 200 -and $r.Body.data.name -eq "Apollo 11 $sfx" -and $r.Body.data.updatedAt)
Check 'update: member PM -> 403' ((Req PUT "/api/projects/$P1" @{ name = 'Hijack' } $tok.pm2).Code -eq 403)
Check 'update: non-member PM -> 404' ((Req PUT "/api/projects/$P1" @{ name = 'Hijack' } $tok.pm3).Code -eq 404)
Check 'update: member TM -> 403' ((Req PUT "/api/projects/$P1" @{ name = 'Hijack' } $tok.tm1).Code -eq 403)
Check 'update: rename to owner''s other project name -> 409' ((Req PUT "/api/projects/$P1" @{ name = "GEMINI $sfx" } $tok.pm1).Code -eq 409)
Check 'update: keep own name (case change) -> 200' ((Req PUT "/api/projects/$P1" @{ name = "APOLLO 11 $sfx" } $tok.pm1).Code -eq 200)
Check 'update: Admin on PM''s project -> 200' ((Req PUT "/api/projects/$P1" @{ name = "Apollo 11 $sfx"; description = 'By admin' } $A).Code -eq 200)

# --- Seed tasks directly (task endpoints come later)
$t = @{ live1 = [guid]::NewGuid(); live2 = [guid]::NewGuid(); old = [guid]::NewGuid(); other = [guid]::NewGuid() }
$ins = "INSERT INTO Tasks (Id, ProjectId, Title, Status, Priority, AssigneeId, CreatedById, CreatedAt, IsDeleted, DeletedAt) VALUES " +
  "('$($t.live1)', '$P1', N'Live assigned', 0, 1, '$($ids.tm1)', '$($ids.pm1)', SYSDATETIMEOFFSET(), 0, NULL)," +
  "('$($t.live2)', '$P1', N'Live unassigned', 1, 2, NULL, '$($ids.pm1)', SYSDATETIMEOFFSET(), 0, NULL)," +
  "('$($t.old)', '$P1', N'Deleted long ago', 0, 0, NULL, '$($ids.pm1)', SYSDATETIMEOFFSET(), 1, '2020-01-01T00:00:00+00:00')," +
  "('$($t.other)', '$P2', N'Other project', 0, 0, '$($ids.tm1)', '$($ids.pm1)', SYSDATETIMEOFFSET(), 0, NULL)"
Sql $ins | Out-Null
Check 'tasks: P1 taskCount = 2 (pre-deleted task excluded by query filter)' ((Req GET "/api/projects/$P1" -bearer $tok.pm1).Body.data.taskCount -eq 2)

# --- Remove member unassigns their tasks in this project only
$r = Req DELETE "/api/projects/$P1/members/$($ids.tm1)" -bearer $tok.pm1
$a1 = Sql "SELECT CONCAT(COALESCE(CONVERT(varchar(36), AssigneeId), 'NULL'), ',', IIF(UpdatedAt IS NULL, 'noupd', 'upd')) FROM Tasks WHERE Id = '$($t.live1)'"
$a2 = Sql "SELECT CONVERT(varchar(36), AssigneeId) FROM Tasks WHERE Id = '$($t.other)'"
Check 'members: remove tm1 -> 200, no longer listed' ($r.Code -eq 200 -and $r.Body.data.members.userId -notcontains $ids.tm1)
Check 'members: tm1''s task in P1 unassigned, UpdatedAt stamped' ($a1 -eq 'NULL,upd') "$a1"
Check 'members: tm1''s task in P2 untouched' ($a2 -eq $ids.tm1)
Check 'members: tm1 can no longer see P1 -> 404' ((Req GET "/api/projects/$P1" -bearer $tok.tm1).Code -eq 404)
Check 'members: remove non-member -> 404' ((Req DELETE "/api/projects/$P1/members/$($ids.tm2)" -bearer $tok.pm1).Code -eq 404)
$r = Req DELETE "/api/projects/$P1/members/$($ids.pm1)" -bearer $tok.pm1
Check 'members: remove the owner -> 409, owner still a member' ($r.Code -eq 409 -and (Req GET "/api/projects/$P1" -bearer $tok.pm1).Body.data.members.userId -contains $ids.pm1) $r.Body.message
Check 'members: Admin removing the owner -> 409' ((Req DELETE "/api/projects/$P1/members/$($ids.pm1)" -bearer $A).Code -eq 409)
$null = Req POST "/api/projects/$P1/members" @{ userId = $ids.tm1 } $tok.pm1
Sql "UPDATE Tasks SET AssigneeId = '$($ids.tm1)' WHERE Id = '$($t.live1)'" | Out-Null

# --- Atomicity: if the task update fails, the project delete must roll back too
SqlDdl "CREATE TRIGGER dbo.trg_test_block_task_softdelete ON dbo.Tasks AFTER UPDATE AS BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE IsDeleted = 1 AND Title = N'Live unassigned') THROW 50001, N'test trigger: block task soft delete', 1; END"
Check 'rollback: test trigger installed' ((Sql "SELECT COUNT(*) FROM sys.triggers WHERE name = 'trg_test_block_task_softdelete'") -eq '1')
try { $r = Req DELETE "/api/projects/$P1" -bearer $tok.pm1 } finally { SqlDdl "DROP TRIGGER dbo.trg_test_block_task_softdelete" }
$state = Sql "SELECT CONCAT((SELECT CAST(IsDeleted AS int) FROM Projects WHERE Id = '$P1'), ',', (SELECT COUNT(*) FROM Tasks WHERE ProjectId = '$P1' AND IsDeleted = 0))"
Check 'rollback: task update fails -> 500, project AND tasks still live' ($r.Code -eq 500 -and $state -eq '0,2') "http=$($r.Code) projectDeleted,liveTasks=$state"
Check 'rollback: no trigger left behind' ((Sql "SELECT COUNT(*) FROM sys.triggers WHERE name = 'trg_test_block_task_softdelete'") -eq '0')

# --- Soft delete permissions
Check 'delete: member PM -> 403' ((Req DELETE "/api/projects/$P1" -bearer $tok.pm2).Code -eq 403)
Check 'delete: non-member PM -> 404' ((Req DELETE "/api/projects/$P1" -bearer $tok.pm3).Code -eq 404)
Check 'delete: member TM -> 403' ((Req DELETE "/api/projects/$P1" -bearer $tok.tm1).Code -eq 403)

# --- Soft delete + cascade
$logMark = if ($ServerLog) { @(Get-Content $ServerLog).Count } else { 0 }
$r = Req DELETE "/api/projects/$P1" -bearer $tok.pm1
Check 'delete: owner -> 200' ($r.Code -eq 200 -and $r.Body.message -eq 'Project deleted.')
$p = Sql "SELECT CONCAT(CAST(IsDeleted AS int), ',', IIF(DeletedAt IS NULL, 'null', 'set'), ',', CONVERT(varchar(36), DeletedById)) FROM Projects WHERE Id = '$P1'"
Check 'db: project row kept, IsDeleted=1, DeletedAt set, DeletedById = owner' ($p -eq "1,set,$($ids.pm1)") "$p"
$pDel = Sql "SELECT CONVERT(varchar(33), DeletedAt, 126) FROM Projects WHERE Id = '$P1'"
$rows = Sql "SELECT CONCAT(Title, ',', CAST(IsDeleted AS int), ',', CONVERT(varchar(33), DeletedAt, 126), ',', COALESCE(CONVERT(varchar(36), DeletedById), 'null')) FROM Tasks WHERE ProjectId = '$P1' ORDER BY Title"
"       tasks: " + ($rows -join ' | ')
$live = @($rows | Where-Object { $_ -like 'Live*' })
Check 'cascade: both live tasks soft-deleted with project''s DeletedAt and DeletedById' ($live.Count -eq 2 -and -not ($live | Where-Object { $_ -notlike "*,1,$pDel,$($ids.pm1)" }))
Check 'cascade: already-deleted task keeps its original DeletedAt (2020)' (@($rows | Where-Object { $_ -like 'Deleted long ago,1,2020-01-01*' }).Count -eq 1)
Check 'cascade: rows not hard-deleted (3 task rows, 3 member rows kept)' ($rows.Count -eq 3 -and (Sql "SELECT COUNT(*) FROM ProjectMembers WHERE ProjectId = '$P1'") -eq '3')
Check 'cascade: other project''s task untouched' ((Sql "SELECT CAST(IsDeleted AS int) FROM Tasks WHERE Id = '$($t.other)'") -eq '0')
if ($ServerLog) {
  Start-Sleep -Milliseconds 500
  $lines = @(Get-Content $ServerLog | Select-Object -Skip $logMark)
  $iProj = ($lines | Select-String -Pattern 'UPDATE \[Projects\]' | Select-Object -First 1).LineNumber
  $iTask = ($lines | Select-String -Pattern 'UPDATE \[Tasks\]' | Select-Object -First 1).LineNumber
  $begins = @($lines | Select-String -Pattern 'Began transaction' | Where-Object { $_.LineNumber -lt [math]::Min($iProj, $iTask) })
  $commits = @($lines | Select-String -Pattern 'Committed transaction' | Where-Object { $_.LineNumber -gt [math]::Max($iProj, $iTask) })
  $between = @($lines[([math]::Min($iProj, $iTask))..([math]::Max($iProj, $iTask))] | Select-String -Pattern 'Committed transaction|Began transaction')
  Check 'transaction: project + task UPDATEs inside one Began..Committed pair' ($iProj -and $iTask -and $begins.Count -ge 1 -and $commits.Count -ge 1 -and $between.Count -eq 0) "projLine=$iProj taskLine=$iTask"
  $upd = @($lines | Select-String -Pattern '^UPDATE \[(Projects|Tasks)\] SET' | ForEach-Object { ($_.Line -replace '@p\d+', '@p').Trim() } | Select-Object -Unique)
  "       update statements: " + ($upd -join ' || ')
  Check 'soft delete writes only IsDeleted/DeletedAt/DeletedById/UpdatedAt' ($upd.Count -ge 1 -and -not ($upd | Where-Object { $_ -match '\[(Name|Title|Description|OwnerId|CreatedAt|AssigneeId|Status)\] =' }))
}

# --- After delete
Check 'after: owner GET -> 404' ((Req GET "/api/projects/$P1" -bearer $tok.pm1).Code -eq 404)
Check 'after: Admin GET -> 404' ((Req GET "/api/projects/$P1" -bearer $A).Code -eq 404)
Check 'after: not in lists' (((Req GET '/api/projects?pageSize=100' -bearer $tok.pm1).Body.data.items.id -notcontains $P1) -and ((Req GET '/api/projects?pageSize=100' -bearer $A).Body.data.items.id -notcontains $P1))
Check 'after: delete again -> 404' ((Req DELETE "/api/projects/$P1" -bearer $tok.pm1).Code -eq 404)
Check 'after: update / add member -> 404' ((Req PUT "/api/projects/$P1" @{ name = 'Z' } $tok.pm1).Code -eq 404 -and (Req POST "/api/projects/$P1/members" @{ userId = $ids.tm2 } $tok.pm1).Code -eq 404)
Check 'after: deleted name can be reused by owner -> 201' ((Req POST /api/projects @{ name = "Apollo 11 $sfx" } $tok.pm1).Code -eq 201)
Check 'after: tm1 (still member row) cannot see it -> list without P1' ((Req GET '/api/projects?pageSize=100' -bearer $tok.tm1).Body.data.items.id -notcontains $P1)

# --- Cleanup (hard delete test data)
$idList = ($ids.Values | ForEach-Object { "'$_'" }) -join ','
Sql "DELETE t FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE p.OwnerId IN ($idList) OR p.Name LIKE N'%$sfx%'; DELETE m FROM ProjectMembers m JOIN Projects p ON p.Id = m.ProjectId WHERE p.OwnerId IN ($idList) OR p.Name LIKE N'%$sfx%'; DELETE FROM Projects WHERE OwnerId IN ($idList) OR Name LIKE N'%$sfx%'; DELETE FROM RefreshTokens WHERE UserId IN ($idList); DELETE FROM Users WHERE Id IN ($idList)" | Out-Null
Check 'cleanup: test users and projects removed' ((Sql "SELECT COUNT(*) FROM Users WHERE Email LIKE '%$sfx%'") -eq '0' -and (Sql "SELECT COUNT(*) FROM Projects WHERE Name LIKE N'%$sfx%'") -eq '0')
Complete-Suite
