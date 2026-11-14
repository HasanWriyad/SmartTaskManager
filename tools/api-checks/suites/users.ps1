# End-to-end test of /api/users against a running API (https profile).
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'users'
$db = @('-S', '(localdb)\MSSQLLocalDB', '-d', $ctx.Database, '-h', '-1', '-W')


# --- Setup
$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
$adminEmail = ($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', ''
$adminPw = ($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', ''
$sfx = "u$(Get-Random -Maximum 99999)"; $pw = 'Str0ng!Passw0rd'
$names = 'pm', 'tm1', 'tm2', 'adm2'
$ids = @{}
foreach ($nm in $names) { $r = Req POST /api/auth/register @{ email = "$nm.$sfx@example.com"; fullName = "Test $nm $sfx"; password = $pw }; $ids[$nm] = $r.Body.data.id }
Check 'setup: 4 test users registered' (@($ids.Values | Where-Object { $_ }).Count -eq 4)
$A = Login $adminEmail $adminPw
$adminId = (Req GET "/api/users?search=$adminEmail" -bearer $A).Body.data.items[0].id
Check 'setup: seeded Admin logged in' ([bool]$A -and [bool]$adminId)

# --- List (Admin)
$r = Req GET "/api/users?search=$sfx" -bearer $A
Check 'list: search by suffix -> 4 users' ($r.Code -eq 200 -and $r.Body.data.totalCount -eq 4) "total=$($r.Body.data.totalCount)"
$fields = ($r.Body.data.items[0].PSObject.Properties.Name) -join ','
Check 'list: item fields (no password hash)' ($fields -eq 'id,email,fullName,role,isActive,createdAt,updatedAt') $fields
Check 'list: raw JSON has no hash/password anywhere' ($r.Text -notmatch '(?i)password|hash|AQAAAA')
$r = Req GET "/api/users?search=$sfx&sortBy=email&sortDirection=desc&pageSize=3&page=1" -bearer $A
$emails = $r.Body.data.items.email
Check 'list: sortBy=email desc, pageSize=3 -> 3 items, sorted, hasNextPage' ($emails.Count -eq 3 -and ($emails -join ',') -eq (($emails | Sort-Object -Descending) -join ',') -and $r.Body.data.hasNextPage -and $r.Body.data.totalPages -eq 2) ($emails -join ',')
$r = Req GET "/api/users?search=$sfx&sortBy=email&sortDirection=desc&pageSize=3&page=2" -bearer $A
Check 'list: page 2 -> remaining 1 item, no next page' ($r.Body.data.items.Count -eq 1 -and -not $r.Body.data.hasNextPage -and $r.Body.data.hasPreviousPage)
$r = Req GET "/api/users?search=$($sfx.ToUpper())" -bearer $A
Check 'list: search is case-insensitive' ($r.Body.data.totalCount -eq 4)
$r = Req GET "/api/users?role=Admin" -bearer $A
Check 'list: filter role=Admin includes seeded Admin only-admins' ($r.Code -eq 200 -and ($r.Body.data.items | Where-Object { $_.role -ne 'Admin' }).Count -eq 0 -and $r.Body.data.items.id -contains $adminId)
foreach ($bad in 'role=1', 'sortBy=passwordHash', 'pageSize=500', 'page=0', 'role=Bogus', 'sortDirection=sideways') {
  $r = Req GET "/api/users?$bad" -bearer $A
  $keys = @($r.Body.errors.PSObject.Properties.Name)
  Check "list: invalid $bad -> 400, camelCase error key" ($r.Code -eq 400 -and $r.Body.success -eq $false -and $keys.Count -gt 0 -and -not ($keys | Where-Object { $_ -cmatch '^[A-Z]' }))((@($r.Body.errors.PSObject.Properties | ForEach-Object { "$($_.Name): $($_.Value -join ' ')" }) -join '; '))
}

# --- Role change
$r = Req PUT "/api/users/$($ids.pm)/role" @{ role = 'ProjectManager' } $A
Check 'role: TeamMember -> ProjectManager -> 200' ($r.Code -eq 200 -and $r.Body.data.role -eq 'ProjectManager' -and $r.Body.data.updatedAt)
$r = Req PUT "/api/users/$($ids.pm)/role" @{ role = 'ProjectManager' } $A
Check 'role: same role again -> 200 no-op' ($r.Code -eq 200 -and $r.Body.data.role -eq 'ProjectManager')
foreach ($body in '{"role":"Bogus"}', '{"role":99}', '{"role":2}', '{}') {
  $r = Req PUT "/api/users/$($ids.tm1)/role" $body $A
  Check "role: body $body -> 400" ($r.Code -eq 400)
}
$r = Req PUT "/api/users/$([guid]::NewGuid())/role" @{ role = 'TeamMember' } $A
Check 'role: unknown user -> 404' ($r.Code -eq 404) $r.Body.message
$r = Req PUT "/api/users/$adminId/role" @{ role = 'TeamMember' } $A
Check 'role: Admin changing own role -> 403' ($r.Code -eq 403) $r.Body.message

# --- ProjectManager and TeamMember access
$PM = Login "pm.$sfx@example.com" $pw
$TM = Login "tm1.$sfx@example.com" $pw
Check 'PM: list users -> 403' ((Req GET /api/users -bearer $PM).Code -eq 403)
Check 'PM: change role -> 403' ((Req PUT "/api/users/$($ids.tm1)/role" @{ role = 'Admin' } $PM).Code -eq 403)
$r = Req GET "/api/users/lookup?search=$sfx" -bearer $PM
$lf = ($r.Body.data.items[0].PSObject.Properties.Name) -join ','
Check 'PM: lookup -> 200 with only id,fullName,email' ($r.Code -eq 200 -and $lf -eq 'id,fullName,email' -and $r.Body.data.totalCount -eq 4) "fields=$lf total=$($r.Body.data.totalCount)"
Check 'Admin: lookup -> 200' ((Req GET "/api/users/lookup?search=$sfx" -bearer $A).Code -eq 200)
Check 'TeamMember: lookup -> 403' ((Req GET /api/users/lookup -bearer $TM).Code -eq 403)
Check 'TeamMember: list -> 403' ((Req GET /api/users -bearer $TM).Code -eq 403)
Check 'lookup: pageSize=51 -> 400 (max 50)' ((Req GET '/api/users/lookup?pageSize=51' -bearer $PM).Code -eq 400)
Check 'lookup: sortBy=role -> 400 (not allowed for lookup)' ((Req GET '/api/users/lookup?sortBy=role' -bearer $PM).Code -eq 400)
Check 'anonymous: lookup -> 401' ((Req GET /api/users/lookup).Code -eq 401)

# --- Status
$jar = Join-Path $tmp 'tm2.txt'; $null = Login "tm2.$sfx@example.com" $pw $jar
$r = Req PUT "/api/users/$($ids.tm2)/status" '{}' $A
Check 'status: body {} -> 400 (isActive required, not "false")' ($r.Code -eq 400 -and $r.Body.errors.isActive)
$r = Req PUT "/api/users/$($ids.tm2)/status" @{ isActive = $false } $A
Check 'status: deactivate tm2 -> 200 isActive=false' ($r.Code -eq 200 -and $r.Body.data.isActive -eq $false)
$active = sqlcmd @db -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM RefreshTokens WHERE UserId = '$($ids.tm2)' AND RevokedAt IS NULL"
Check 'status: deactivation revoked all tm2 refresh tokens' ((($active | Where-Object { $_ -match '^\d+$' }) -as [int]) -eq 0)
Check 'status: deactivated user refresh -> 401' ((Req POST /api/auth/refresh -jar $jar).Code -eq 401)
$r = Req GET "/api/users/lookup?search=$sfx" -bearer $PM
Check 'lookup: excludes deactivated user' ($r.Body.data.items.id -notcontains $ids.tm2 -and $r.Body.data.totalCount -eq 3)
$r = Req GET "/api/users?search=$sfx&isActive=false" -bearer $A
Check 'list: isActive=false filter -> only tm2' ($r.Body.data.totalCount -eq 1 -and $r.Body.data.items[0].id -eq $ids.tm2)
$r = Req PUT "/api/users/$($ids.tm2)/status" @{ isActive = $true } $A
Check 'status: reactivate tm2 -> 200, can log in again' ($r.Code -eq 200 -and $r.Body.data.isActive -and (Login "tm2.$sfx@example.com" $pw))
$r = Req PUT "/api/users/$adminId/status" @{ isActive = $false } $A
Check 'status: Admin deactivating self -> 403' ($r.Code -eq 403) $r.Body.message
Check 'status: Admin "activating" self -> 200 no-op' ((Req PUT "/api/users/$adminId/status" @{ isActive = $true } $A).Code -eq 200)

# --- Last active Admin
$r = Req PUT "/api/users/$($ids.adm2)/role" @{ role = 'Admin' } $A
$B = Login "adm2.$sfx@example.com" $pw
Check 'last-admin setup: second Admin B promoted and logged in' ($r.Code -eq 200 -and [bool]$B)
$otherAdmins = sqlcmd @db -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Users WHERE Role = 1 AND IsActive = 1 AND Id NOT IN ('$adminId','$($ids.adm2)')"
$onlyTwo = ((($otherAdmins | Where-Object { $_ -match '^\d+$' }) -as [int]) -eq 0)
Check 'last-admin setup: A and B are the only active Admins' $onlyTwo
if ($onlyTwo) {
  $r = Req PUT "/api/users/$($ids.adm2)/status" @{ isActive = $false } $A
  Check 'A deactivates B -> 200 (A remains)' ($r.Code -eq 200)
  # D6: deactivation takes effect immediately, so B's still-unexpired token is rejected outright.
  $r = Req PUT "/api/users/$adminId/role" @{ role = 'TeamMember' } $B
  Check "B's token after deactivation -> 401 (cannot demote A)" ($r.Code -eq 401) $r.Body.message
  $r = Req PUT "/api/users/$adminId/status" @{ isActive = $false } $B
  Check "B's token after deactivation -> 401 (cannot deactivate A)" ($r.Code -eq 401)
  Check 'A is still an active Admin' ((sqlcmd @db -Q "SET NOCOUNT ON; SELECT CONCAT(Role, ',', CAST(IsActive AS int)) FROM Users WHERE Id = '$adminId'") -contains '1,1')
  Check 'A reactivates B -> 200' ((Req PUT "/api/users/$($ids.adm2)/status" @{ isActive = $true } $A).Code -eq 200)
  $B = Login "adm2.$sfx@example.com" $pw

  # Race: A demotes B while B demotes A. Without serialization both could pass the "another admin exists" check.
  $hA = Join-Path $tmp 'ra.json'; Set-Content $hA '{"role":"TeamMember"}' -NoNewline -Encoding ascii
  $race = curl.exe -sk --parallel --parallel-immediate -w "%{http_code}`n" -o NUL `
    -X PUT "$Base/api/users/$($ids.adm2)/role" -H "Authorization: Bearer $A" -H 'Content-Type: application/json' --data-binary "@$hA" --next `
    -sk -o NUL -w "%{http_code}`n" -X PUT "$Base/api/users/$adminId/role" -H "Authorization: Bearer $B" -H 'Content-Type: application/json' --data-binary "@$hA"
  $codes = ($race | Where-Object { $_ } | Sort-Object) -join ','
  $admins = sqlcmd @db -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Users WHERE Role = 1 AND IsActive = 1"
  $adminCount = (($admins | Where-Object { $_ -match '^\d+$' }) -as [int])
  # The loser gets 409 (last-admin rule) or, if the winner committed before its token was checked, 401.
  Check 'race: mutual demotion -> one 200, one 409/401, an Admin remains' (($codes -eq '200,409' -or $codes -eq '200,401') -and $adminCount -ge 1) "codes=$codes activeAdmins=$adminCount"
}

# --- D6: role changes take effect immediately; refresh issues a token with the new role
sqlcmd @db -Q "SET NOCOUNT ON; UPDATE Users SET Role = 1, IsActive = 1 WHERE Id = '$adminId'" | Out-Null
$A = Login $adminEmail $adminPw
$jar = Join-Path $tmp 'pm-jar.txt'
$PMold = Login "pm.$sfx@example.com" $pw $jar
Check 'D6 setup: PM can use lookup' ((Req GET /api/users/lookup -bearer $PMold).Code -eq 200)
$null = Req PUT "/api/users/$($ids.pm)/role" @{ role = 'TeamMember' } $A
$r = Req GET /api/dashboard -bearer $PMold
Check 'D6: old token after demotion -> 401 on any endpoint' ($r.Code -eq 401 -and $r.Body.success -eq $false) $r.Body.message
$r = Req POST /api/auth/refresh -jar $jar
Check 'D6: refresh -> 200 with the new role (TeamMember)' ($r.Code -eq 200 -and $r.Body.data.user.role -eq 'TeamMember')
$PMnew = $r.Body.data.accessToken
Check 'D6: new token works, and has TeamMember rights only' ((Req GET /api/dashboard -bearer $PMnew).Code -eq 200 -and (Req GET /api/users/lookup -bearer $PMnew).Code -eq 403)
$null = Req PUT "/api/users/$($ids.pm)/status" @{ isActive = $false } $A
Check 'D6: token after deactivation -> 401; refresh -> 401' ((Req GET /api/dashboard -bearer $PMnew).Code -eq 401 -and (Req POST /api/auth/refresh -jar $jar).Code -eq 401)
Check 'D6: Admin token unaffected by others'' changes' ((Req GET /api/users -bearer $A).Code -eq 200)
$authLines = @(Select-String -Path $ServerLog -Pattern 'Rejected access token for user .*: (role changed|user is deactivated)').Count
Check 'D6: rejections logged with the reason' ($authLines -ge 2) "lines=$authLines"

# --- Logs and cleanup
sqlcmd @db -Q "SET NOCOUNT ON; UPDATE Users SET Role = 1, IsActive = 1 WHERE Id = '$adminId'" | Out-Null
$idList = ($ids.Values | ForEach-Object { "'$_'" }) -join ','
sqlcmd @db -Q "SET NOCOUNT ON; DELETE FROM RefreshTokens WHERE UserId IN ($idList); DELETE FROM Users WHERE Id IN ($idList)" | Out-Null
$left = sqlcmd @db -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM Users WHERE Email LIKE '%$sfx%'"
$adm = sqlcmd @db -Q "SET NOCOUNT ON; SELECT CONCAT(Role, ',', IsActive) FROM Users WHERE Id = '$adminId'"
Check 'cleanup: test users removed, seeded Admin is Admin and active' (((($left | Where-Object { $_ -match '^\d+$' }) -as [int]) -eq 0) -and ($adm -contains '1,1'))
Complete-Suite
