# Search, filters, sorting and paging for GET /api/projects, GET /api/projects/{id}/tasks and GET /api/tasks.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'lists'

function Names($r, $prop) { ($r.Body.data.items | ForEach-Object { $_.$prop }) -join ',' }

# --- Setup
$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
$A = Login (($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', '') (($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', '')
$sfx = "l$(Get-Random -Maximum 99999)"; $pw = 'Str0ng!Passw0rd'
$ids = @{}; $tok = @{}
foreach ($nm in 'pm1', 'pm2', 'tm1') { $ids[$nm] = (Req POST /api/auth/register @{ email = "$nm.$sfx@example.com"; fullName = "Test $nm $sfx"; password = $pw }).Body.data.id }
foreach ($nm in 'pm1', 'pm2') { $null = Req PUT "/api/users/$($ids[$nm])/role" @{ role = 'ProjectManager' } $A }
foreach ($nm in 'pm1', 'pm2', 'tm1') { $tok[$nm] = Login "$nm.$sfx@example.com" $pw }

$alpha = (Req POST /api/projects @{ name = "Alpha $sfx"; description = 'Rocket engines' } $tok.pm1).Body.data.id
Start-Sleep -Milliseconds 30
$bravo = (Req POST /api/projects @{ name = "Bravo $sfx"; description = 'Garden' } $tok.pm1).Body.data.id
Start-Sleep -Milliseconds 30
$charlie = (Req POST /api/projects @{ name = "Charlie $sfx" } $tok.pm1).Body.data.id
$delta = (Req POST /api/projects @{ name = "Delta $sfx (pm2)" } $tok.pm2).Body.data.id
$null = Req POST "/api/projects/$alpha/members" @{ userId = $ids.tm1 } $tok.pm1

$d = { param($days) [DateTime]::UtcNow.Date.AddDays($days).ToString('yyyy-MM-dd') }
$t = [ordered]@{}
$specs = @(
  @{ k = 'a'; title = "Design rocket"; priority = 'High'; dueDate = (& $d 5); assigneeId = $ids.tm1 },
  @{ k = 'b'; title = "Build engine"; description = 'Rocket fuel lines'; priority = 'Critical'; dueDate = (& $d 2); assigneeId = $ids.pm1 },
  @{ k = 'c'; title = "Write docs"; priority = 'Low' },
  @{ k = 'd'; title = "Test launch"; priority = 'Medium'; dueDate = (& $d 10); assigneeId = $ids.tm1 },
  @{ k = 'e'; title = "Cancelled idea"; priority = 'Medium'; dueDate = (& $d 1) }
)
foreach ($s in $specs) { $k = $s.k; $s.Remove('k'); $t[$k] = (Req POST "/api/projects/$alpha/tasks" $s $tok.pm1).Body.data.id; Start-Sleep -Milliseconds 30 }
$null = Req PUT "/api/projects/$alpha/tasks/$($t.b)/status" @{ status = 'InProgress' } $tok.pm1
$null = Req PUT "/api/projects/$alpha/tasks/$($t.c)/status" @{ status = 'Completed' } $tok.pm1
$null = Req PUT "/api/projects/$alpha/tasks/$($t.e)/status" @{ status = 'Cancelled' } $tok.pm1
$null = Req POST "/api/projects/$bravo/tasks" @{ title = 'Plant seeds' } $tok.pm1
$key = @{}; foreach ($k in $t.Keys) { $key[$t[$k]] = $k }
function Order($r) { ($r.Body.data.items | ForEach-Object { $key[$_.id] }) -join '' }
Check 'setup: 4 projects (3 for pm1), 5 tasks in Alpha, 1 in Bravo' (@($t.Values | Where-Object { $_ }).Count -eq 5 -and [bool]$charlie)

# ===== Projects
$r = Req GET "/api/projects?search=$sfx" -bearer $tok.pm1
Check 'projects: default order = name asc; only visible (no pm2 project)' ($r.Code -eq 200 -and $r.Body.data.totalCount -eq 3 -and (Names $r 'name') -eq "Alpha $sfx,Bravo $sfx,Charlie $sfx") (Names $r 'name')
Check 'projects: paged shape (page, pageSize, totalPages, flags)' ($r.Body.data.page -eq 1 -and $r.Body.data.pageSize -eq 20 -and $r.Body.data.totalPages -eq 1 -and -not $r.Body.data.hasNextPage -and -not $r.Body.data.hasPreviousPage)
Check 'projects: search matches description ("rocket" -> Alpha)' ((Names (Req GET "/api/projects?search=rocket%20engines" -bearer $tok.pm1) 'name') -eq "Alpha $sfx")
Check 'projects: search is case-insensitive ("GARDEN" -> Bravo)' ((Names (Req GET "/api/projects?search=GARDEN" -bearer $tok.pm1) 'name') -eq "Bravo $sfx")
Check 'projects: sortBy=name desc' ((Names (Req GET "/api/projects?search=$sfx&sortBy=name&sortDirection=desc" -bearer $tok.pm1) 'name') -eq "Charlie $sfx,Bravo $sfx,Alpha $sfx")
$r = Req GET "/api/projects?search=$sfx&sortBy=taskCount&sortDirection=desc" -bearer $tok.pm1
Check 'projects: sortBy=taskCount desc (5,1,0)' ((Names $r 'name') -eq "Alpha $sfx,Bravo $sfx,Charlie $sfx" -and (Names $r 'taskCount') -eq '5,1,0') (Names $r 'taskCount')
Check 'projects: sortBy=taskCount asc (0,1,5)' ((Names (Req GET "/api/projects?search=$sfx&sortBy=taskCount" -bearer $tok.pm1) 'taskCount') -eq '0,1,5')
Check 'projects: sortBy=createdAt desc (newest first)' ((Names (Req GET "/api/projects?search=$sfx&sortBy=CREATEDAT&sortDirection=desc" -bearer $tok.pm1) 'name') -eq "Charlie $sfx,Bravo $sfx,Alpha $sfx")
$r = Req GET "/api/projects?search=$sfx&pageSize=2&page=2" -bearer $tok.pm1
Check 'projects: pageSize=2 page=2 -> [Charlie], hasPrevious, 2 pages' ((Names $r 'name') -eq "Charlie $sfx" -and $r.Body.data.totalPages -eq 2 -and $r.Body.data.hasPreviousPage -and -not $r.Body.data.hasNextPage -and $r.Body.data.totalCount -eq 3)
$r = Req GET "/api/projects?search=$sfx&page=9" -bearer $tok.pm1
Check 'projects: page past the end -> 200, empty items, totals kept' ($r.Code -eq 200 -and @($r.Body.data.items).Count -eq 0 -and $r.Body.data.totalCount -eq 3)
$r = Req GET "/api/projects?search=$sfx" -bearer $tok.tm1
Check 'projects: TM sees only member project; totalCount honours visibility' ($r.Body.data.totalCount -eq 1 -and (Names $r 'name') -eq "Alpha $sfx")
Check 'projects: Admin search sees all 4' ((Req GET "/api/projects?search=$sfx" -bearer $A).Body.data.totalCount -eq 4)
foreach ($bad in 'sortBy=owner', 'pageSize=0', 'pageSize=101', 'page=-1', "search=$('x' * 101)", 'sortDirection=up') {
  $r = Req GET "/api/projects?$bad" -bearer $tok.pm1
  Check "projects: invalid $($bad.Substring(0, [Math]::Min(20, $bad.Length))) -> 400" ($r.Code -eq 400) (($r.Body.errors.PSObject.Properties | ForEach-Object { "$($_.Name): $($_.Value -join ' ')" }) -join '; ')
}

# --- Project filters
$todayStr = [DateTime]::UtcNow.ToString('yyyy-MM-dd')
Check 'projects: ownedByMe=true (pm1) -> own 3' ((Req GET "/api/projects?search=$sfx&ownedByMe=true" -bearer $tok.pm1).Body.data.totalCount -eq 3)
Check 'projects: ownedByMe=false (pm1) -> 0 (Delta is not visible to pm1)' ((Req GET "/api/projects?search=$sfx&ownedByMe=false" -bearer $tok.pm1).Body.data.totalCount -eq 0)
Check 'projects: ownedByMe=false (tm1) -> Alpha (member, not owner)' ((Names (Req GET "/api/projects?search=$sfx&ownedByMe=false" -bearer $tok.tm1) 'name') -eq "Alpha $sfx")
Check 'projects: ownedByMe=true (tm1) -> 0' ((Req GET "/api/projects?search=$sfx&ownedByMe=true" -bearer $tok.tm1).Body.data.totalCount -eq 0)
Check 'projects: ownerId=pm2 (Admin) -> Delta' ((Names (Req GET "/api/projects?search=$sfx&ownerId=$($ids.pm2)" -bearer $A) 'name') -eq "Delta $sfx (pm2)")
Check 'projects: ownerId=pm2 (pm1) -> 0 (filters never widen visibility)' ((Req GET "/api/projects?search=$sfx&ownerId=$($ids.pm2)" -bearer $tok.pm1).Body.data.totalCount -eq 0)
Check 'projects: ownerId=pm1 (Admin) -> 3' ((Req GET "/api/projects?search=$sfx&ownerId=$($ids.pm1)" -bearer $A).Body.data.totalCount -eq 3)
Check 'projects: createdFrom=createdTo=today -> 3 (inclusive)' ((Req GET "/api/projects?search=$sfx&createdFrom=$todayStr&createdTo=$todayStr" -bearer $tok.pm1).Body.data.totalCount -eq 3)
Check 'projects: createdFrom=tomorrow -> 0; createdTo=yesterday -> 0' ((Req GET "/api/projects?search=$sfx&createdFrom=$([DateTime]::UtcNow.AddDays(1).ToString('yyyy-MM-dd'))" -bearer $tok.pm1).Body.data.totalCount -eq 0 -and (Req GET "/api/projects?search=$sfx&createdTo=$([DateTime]::UtcNow.AddDays(-1).ToString('yyyy-MM-dd'))" -bearer $tok.pm1).Body.data.totalCount -eq 0)
Check 'projects: combined ownedByMe=true + sortBy=taskCount desc' ((Names (Req GET "/api/projects?search=$sfx&ownedByMe=true&sortBy=taskCount&sortDirection=desc" -bearer $tok.pm1) 'name') -eq "Alpha $sfx,Bravo $sfx,Charlie $sfx")
foreach ($bad in "ownerId=$($ids.pm1)&ownedByMe=true", "createdFrom=$todayStr&createdTo=2020-01-01", "ownerId=$([guid]::Empty)", 'createdFrom=yesterday', 'ownedByMe=maybe') {
  $r = Req GET "/api/projects?$bad" -bearer $tok.pm1
  Check "projects: invalid $($bad.Substring(0, [Math]::Min(26, $bad.Length))) -> 400" ($r.Code -eq 400) (($r.Body.errors.PSObject.Properties | ForEach-Object { "$($_.Name): $($_.Value -join ' ')" }) -join '; ')
}

# ===== Tasks (Alpha): a=High/ToDo/+5/tm1  b=Critical/InProgress/+2/pm1  c=Low/Completed/none  d=Medium/ToDo/+10/tm1  e=Medium/Cancelled/+1
$TasksUrl = "/api/projects/$alpha/tasks"
$r = Req GET $TasksUrl -bearer $tok.tm1
Check 'tasks: default order = priority desc, due asc (none last) -> b a e d c' ($r.Code -eq 200 -and (Order $r) -eq 'baedc' -and $r.Body.data.totalCount -eq 5) (Order $r)
Check 'tasks: default ignores sortDirection' ((Order (Req GET "$($TasksUrl)?sortDirection=desc" -bearer $tok.tm1)) -eq 'baedc')
Check 'tasks: search title or description ("rocket" -> a, b)' ((Order (Req GET "$($TasksUrl)?search=rocket" -bearer $tok.tm1)) -eq 'ba')
Check 'tasks: enum names are case-insensitive (status=todo) -> a d' ((Order (Req GET "$($TasksUrl)?status=todo" -bearer $tok.tm1)) -eq 'ad')
Check 'tasks: filter status=ToDo -> a d' ((Order (Req GET "$($TasksUrl)?status=ToDo" -bearer $tok.tm1)) -eq 'ad')
Check 'tasks: filter priority=Medium -> e d' ((Order (Req GET "$($TasksUrl)?priority=Medium" -bearer $tok.tm1)) -eq 'ed')
Check 'tasks: filter assigneeId=tm1 -> a d' ((Order (Req GET "$($TasksUrl)?assigneeId=$($ids.tm1)" -bearer $tok.tm1)) -eq 'ad')
Check 'tasks: filter dueFrom=+2 dueTo=+5 (inclusive) -> b a' ((Order (Req GET "$($TasksUrl)?dueFrom=$(& $d 2)&dueTo=$(& $d 5)" -bearer $tok.tm1)) -eq 'ba')
Check 'tasks: filter dueFrom=+6 -> d (undated c excluded)' ((Order (Req GET "$($TasksUrl)?dueFrom=$(& $d 6)" -bearer $tok.tm1)) -eq 'd')
Check 'tasks: filter dueTo=+1 -> e' ((Order (Req GET "$($TasksUrl)?dueTo=$(& $d 1)" -bearer $tok.tm1)) -eq 'e')
Check 'tasks: combined status=ToDo&assigneeId=tm1&search=launch -> d' ((Order (Req GET "$($TasksUrl)?status=ToDo&assigneeId=$($ids.tm1)&search=launch" -bearer $tok.tm1)) -eq 'd')
Check 'tasks: sortBy=title asc -> b e a d c' ((Order (Req GET "$($TasksUrl)?sortBy=title" -bearer $tok.tm1)) -eq 'beadc')
Check 'tasks: sortBy=title desc -> c d a e b' ((Order (Req GET "$($TasksUrl)?sortBy=title&sortDirection=desc" -bearer $tok.tm1)) -eq 'cdaeb')
Check 'tasks: sortBy=priority asc (ties by createdAt) -> c d e a b' ((Order (Req GET "$($TasksUrl)?sortBy=priority" -bearer $tok.tm1)) -eq 'cdeab')
Check 'tasks: sortBy=status asc (workflow order) -> a d b c e' ((Order (Req GET "$($TasksUrl)?sortBy=status" -bearer $tok.tm1)) -eq 'adbce')
Check 'tasks: sortBy=dueDate asc (undated last) -> e b a d c' ((Order (Req GET "$($TasksUrl)?sortBy=dueDate" -bearer $tok.tm1)) -eq 'ebadc')
Check 'tasks: sortBy=dueDate desc (undated still last) -> d a b e c' ((Order (Req GET "$($TasksUrl)?sortBy=dueDate&sortDirection=desc" -bearer $tok.tm1)) -eq 'dabec')
Check 'tasks: sortBy=createdAt desc -> e d c b a' ((Order (Req GET "$($TasksUrl)?sortBy=createdAt&sortDirection=desc" -bearer $tok.tm1)) -eq 'edcba')
$r = Req GET "$($TasksUrl)?pageSize=2&page=3" -bearer $tok.tm1
Check 'tasks: pageSize=2 page=3 -> c (default order), 3 pages' ((Order $r) -eq 'c' -and $r.Body.data.totalPages -eq 3 -and $r.Body.data.hasPreviousPage -and -not $r.Body.data.hasNextPage)
$r = Req GET "$($TasksUrl)?pageSize=2&page=2&sortBy=title" -bearer $tok.tm1
Check 'tasks: page 2 of title sort -> a d' ((Order $r) -eq 'ad')
foreach ($bad in 'status=1', 'status=ToDo,InProgress', 'priority=2', 'status=Done', 'priority=Urgent', 'sortBy=assignee', "dueFrom=$(& $d 5)&dueTo=$(& $d 1)", 'dueFrom=not-a-date', "assigneeId=$([guid]::Empty)", 'pageSize=500') {
  $r = Req GET "$($TasksUrl)?$bad" -bearer $tok.tm1
  Check "tasks: invalid $($bad.Substring(0, [Math]::Min(26, $bad.Length))) -> 400" ($r.Code -eq 400 -and -not ($r.Body.errors.PSObject.Properties.Name | Where-Object { $_ -cmatch '^[A-Z$]' })) (($r.Body.errors.PSObject.Properties | ForEach-Object { "$($_.Name): $($_.Value -join ' ')" }) -join '; ')
}
$null = Req DELETE "$TasksUrl/$($t.e)" -bearer $tok.pm1
$r = Req GET $TasksUrl -bearer $tok.tm1
Check 'tasks: soft-deleted task excluded from list and count' ((Order $r) -eq 'badc' -and $r.Body.data.totalCount -eq 4)
Check 'projects: taskCount sort uses live tasks (Alpha now 4)' ((Names (Req GET "/api/projects?search=$sfx&sortBy=taskCount&sortDirection=desc" -bearer $tok.pm1) 'taskCount') -eq '4,1,0')
Check 'tasks: non-member list -> 404' ((Req GET "/api/projects/$bravo/tasks" -bearer $tok.tm1).Code -eq 404)
Check 'tasks: project list rows carry projectName' ((Names (Req GET $TasksUrl -bearer $tok.tm1) 'projectName') -eq (@("Alpha $sfx") * 4 -join ','))

# ===== All tasks (GET /api/tasks): Alpha a b c d (e deleted), Bravo p, Delta x (pm2 only).
# Due dates cannot be set in the past through the API, so overdue cases are set up in SQL.
$t['p'] = @((Req GET "/api/projects/$bravo/tasks" -bearer $tok.pm1).Body.data.items)[0].id
$t['x'] = (Req POST "/api/projects/$delta/tasks" @{ title = 'Delta work' } $tok.pm2).Body.data.id
$key = @{}; foreach ($k in $t.Keys) { $key[$t[$k]] = $k }
function SetDue($k, $days) { Sql "UPDATE Tasks SET DueDate = DATEADD(day, $days, CAST(SYSUTCDATETIME() AS date)) WHERE Id = '$($t[$k])'" | Out-Null }
SetDue 'a' -3; SetDue 'c' -3   # a = High/ToDo overdue; c = Completed, past due but not overdue
# Now: a=High/ToDo/-3/tm1  b=Critical/InProgress/+2/pm1  c=Low/Completed/-3  d=Medium/ToDo/+10/tm1  p=Medium/ToDo/none
$r = Req GET /api/tasks -bearer $tok.pm1
Check 'all tasks: pm1 sees Alpha + Bravo, default order -> b a d p c' ($r.Code -eq 200 -and (Order $r) -eq 'badpc' -and $r.Body.data.totalCount -eq 5) (Order $r)
$rows = @{}; foreach ($i in $r.Body.data.items) { $rows[$key[$i.id]] = $i }
Check 'all tasks: rows carry projectId and projectName' ($rows.a.projectName -eq "Alpha $sfx" -and $rows.p.projectName -eq "Bravo $sfx" -and $rows.p.projectId -eq $bravo -and $rows.a.assigneeName -eq "Test tm1 $sfx")
Check 'all tasks: tm1 (member of Alpha only) -> b a d c' ((Order (Req GET /api/tasks -bearer $tok.tm1)) -eq 'badc')
Check 'all tasks: pm2 sees only Delta -> x' ((Order (Req GET /api/tasks -bearer $tok.pm2)) -eq 'x')
Check 'all tasks: Admin projectId=Delta -> x' ((Order (Req GET "/api/tasks?projectId=$delta" -bearer $A)) -eq 'x')
Check 'all tasks: projectId=Bravo (pm1) -> p' ((Order (Req GET "/api/tasks?projectId=$bravo" -bearer $tok.pm1)) -eq 'p')
$r = Req GET "/api/tasks?projectId=$delta" -bearer $tok.pm1
Check 'all tasks: projectId of an invisible project -> 200, empty' ($r.Code -eq 200 -and $r.Body.data.totalCount -eq 0)
Check 'all tasks: projectId of an unknown project -> 200, empty' ((Req GET "/api/tasks?projectId=$([guid]::NewGuid())" -bearer $tok.pm1).Body.data.totalCount -eq 0)
Check 'all tasks: search title or description ("rocket" -> b a)' ((Order (Req GET '/api/tasks?search=rocket' -bearer $tok.pm1)) -eq 'ba')
Check 'all tasks: status=ToDo -> a d p' ((Order (Req GET '/api/tasks?status=ToDo' -bearer $tok.pm1)) -eq 'adp')
Check 'all tasks: priority=Medium -> d p' ((Order (Req GET '/api/tasks?priority=Medium' -bearer $tok.pm1)) -eq 'dp')
Check 'all tasks: assigneeId=tm1 -> a d' ((Order (Req GET "/api/tasks?assigneeId=$($ids.tm1)" -bearer $tok.pm1)) -eq 'ad')
Check 'all tasks: dueFrom=today -> b d' ((Order (Req GET "/api/tasks?dueFrom=$(& $d 0)" -bearer $tok.pm1)) -eq 'bd')
Check 'all tasks: overdue=true -> a (completed c excluded)' ((Order (Req GET '/api/tasks?overdue=true' -bearer $tok.pm1)) -eq 'a')
Check 'all tasks: overdue=true for tm1 -> a' ((Order (Req GET '/api/tasks?overdue=true' -bearer $tok.tm1)) -eq 'a')
Check 'all tasks: overdue=false is no filter -> 5' ((Req GET '/api/tasks?overdue=false' -bearer $tok.pm1).Body.data.totalCount -eq 5)
$r = Req GET '/api/tasks?pageSize=2&page=2' -bearer $tok.pm1
Check 'all tasks: pageSize=2 page=2 -> d p, 3 pages' ((Order $r) -eq 'dp' -and $r.Body.data.totalPages -eq 3 -and $r.Body.data.hasNextPage)
SetDue 'b' -1; SetDue 'd' -2; $null = Req PUT "/api/projects/$alpha/tasks/$($t.d)/status" @{ status = 'Cancelled' } $tok.pm1
SetDue 'p' 0   # due today: not overdue
Check 'all tasks: overdue includes InProgress, excludes Cancelled and due-today -> b a' ((Order (Req GET '/api/tasks?overdue=true' -bearer $tok.pm1)) -eq 'ba')
Check 'all tasks: overdue=true&status=InProgress -> b' ((Order (Req GET '/api/tasks?overdue=true&status=InProgress' -bearer $tok.pm1)) -eq 'b')
Check 'all tasks: overdue=true&projectId=Bravo -> none' ((Req GET "/api/tasks?overdue=true&projectId=$bravo" -bearer $tok.pm1).Body.data.totalCount -eq 0)
Check 'all tasks: sortBy=dueDate (ties by createdAt) -> a c d b p' ((Order (Req GET '/api/tasks?sortBy=dueDate' -bearer $tok.pm1)) -eq 'acdbp')
Check 'all tasks: sortBy=title desc -> c d p a b' ((Order (Req GET '/api/tasks?sortBy=title&sortDirection=desc' -bearer $tok.pm1)) -eq 'cdpab')
foreach ($bad in "projectId=$([guid]::Empty)", 'projectId=abc', 'overdue=maybe', 'sortBy=project', 'status=1', 'pageSize=0', "dueFrom=$(& $d 5)&dueTo=$(& $d 1)") {
  $r = Req GET "/api/tasks?$bad" -bearer $tok.pm1
  Check "all tasks: invalid $($bad.Substring(0, [Math]::Min(26, $bad.Length))) -> 400" ($r.Code -eq 400 -and -not ($r.Body.errors.PSObject.Properties.Name | Where-Object { $_ -cmatch '^[A-Z$]' })) (($r.Body.errors.PSObject.Properties | ForEach-Object { "$($_.Name): $($_.Value -join ' ')" }) -join '; ')
}
Check 'all tasks: anonymous -> 401' ((Req GET /api/tasks).Code -eq 401)
$null = Req DELETE "/api/projects/$bravo" -bearer $tok.pm1
Check 'all tasks: tasks of a deleted project disappear -> b a d c' ((Order (Req GET '/api/tasks?sortBy=title' -bearer $tok.pm1)) -eq 'badc' -and (Req GET /api/tasks -bearer $tok.pm1).Body.data.totalCount -eq 4)

# --- Cleanup
$idList = ($ids.Values | ForEach-Object { "'$_'" }) -join ','
Sql "DELETE t FROM Tasks t JOIN Projects p ON p.Id = t.ProjectId WHERE p.OwnerId IN ($idList); DELETE m FROM ProjectMembers m JOIN Projects p ON p.Id = m.ProjectId WHERE p.OwnerId IN ($idList); DELETE FROM Projects WHERE OwnerId IN ($idList); DELETE FROM RefreshTokens WHERE UserId IN ($idList); DELETE FROM Users WHERE Id IN ($idList)" | Out-Null
Check 'cleanup: test data removed' ((Sql "SELECT COUNT(*) FROM Users WHERE Email LIKE '%.$sfx@example.com'") -eq '0' -and (Sql "SELECT COUNT(*) FROM Projects WHERE Name LIKE N'%$sfx%'") -eq '0')
Complete-Suite
