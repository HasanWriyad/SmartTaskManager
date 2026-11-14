# Rate limiting, health check and body-size limit. Run with RateLimiting__GlobalPerMinute=60
# (auth 5/min and AI 10/min are the real defaults) and a blank AI key (fallback, no external calls).
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'rate-limits'
function Is429($r) { $r.Code -eq 429 -and $r.Body.success -eq $false -and $r.Body.traceId -and [int]$r.RetryAfter -ge 1 -and $r.Body.message -match "try again in $($r.RetryAfter) seconds" }

$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
$adminEmail = ($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', ''
$adminPw = ($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', ''
$sfx = "o$(Get-Random -Maximum 99999)"; $pw = 'Str0ng!Passw0rd'

# --- Health: anonymous, ApiResponse shape, includes the database, never rate limited
$h = Req GET /health
Check 'health: anonymous 200, ApiResponse, Healthy' ($h.Code -eq 200 -and $h.Body.success -and $h.Body.data.status -eq 'Healthy' -and $h.Body.message -eq 'Healthy')
Check 'health: database check present and Healthy' (@($h.Body.data.checks | Where-Object { $_.name -eq 'database' -and $_.status -eq 'Healthy' }).Count -eq 1) (($h.Body.data.checks | ForEach-Object { "$($_.name)=$($_.status) $($_.durationMs)ms" }) -join '; ')
$codes = 1..70 | ForEach-Object { (Req GET /health).Code }
Check 'health: 70 rapid calls (> global 60) all 200 - exempt from limits' (@($codes | Where-Object { $_ -ne 200 }).Count -eq 0) "non-200: $(@($codes | Where-Object { $_ -ne 200 }).Count)"

# --- Auth policy: 5 per minute per IP, shared by login and register
$r1 = Req POST /api/auth/register @{ email = "pm.$sfx@example.com"; fullName = "Test pm $sfx"; password = $pw }   # 1
$A = (Req POST /api/auth/login @{ email = $adminEmail; password = $adminPw }).Body.data.accessToken              # 2
$null = Req PUT "/api/users/$($r1.Body.data.id)/role" @{ role = 'ProjectManager' } $A
$PM = (Req POST /api/auth/login @{ email = "pm.$sfx@example.com"; password = $pw }).Body.data.accessToken         # 3
Check 'setup: registered PM, Admin + PM logged in (3 auth calls)' ([bool]$A -and [bool]$PM -and $r1.Code -eq 201)
$c4 = (Req POST /api/auth/login @{ email = 'nobody@example.com'; password = 'Wrong!Passw0rd1' }).Code               # 4 (failed logins count too)
$c5 = (Req POST /api/auth/login @{ email = 'nobody@example.com'; password = 'Wrong!Passw0rd1' }).Code               # 5
$r6 = Req POST /api/auth/login @{ email = $adminEmail; password = $adminPw }                                        # 6
Check 'auth: calls 4-5 processed (401), 6th login -> 429' ($c4 -eq 401 -and $c5 -eq 401 -and (Is429 $r6)) "4=$c4 5=$c5 6=$($r6.Code) Retry-After=$($r6.RetryAfter) '$($r6.Body.message)'"
$r7 = Req POST /api/auth/register @{ email = "late.$sfx@example.com"; fullName = 'Late'; password = $pw }
Check 'auth: register shares the same limit -> 429, user not created' ((Is429 $r7) -and (Sql "SELECT COUNT(*) FROM Users WHERE Email = 'late.$sfx@example.com'") -eq '0')
Check 'auth: refresh/logout not under the auth policy (401/200, not 429)' ((Req POST /api/auth/refresh).Code -eq 401 -and (Req POST /api/auth/logout).Code -eq 200)

# --- AI policy: 10 per minute per user (fallback provider, no external calls)
$aiCodes = 1..11 | ForEach-Object { (Req POST /api/ai/improve-description @{ description = "task $_" } $PM).Code }
$r12 = Req POST /api/ai/improve-description @{ description = 'one more' } $PM
Check 'ai: PM gets 10 x 200, then 429' ((@($aiCodes[0..9] | Where-Object { $_ -eq 200 }).Count -eq 10) -and $aiCodes[10] -eq 429 -and (Is429 $r12)) "codes: $($aiCodes -join ',')"
$ra = Req POST /api/ai/improve-description @{ description = 'admin task' } $A
Check 'ai: limit is per user - Admin from the same IP still 200' ($ra.Code -eq 200 -and $ra.Body.data.source -eq 'fallback')
Check 'ai: other endpoints still open for the limited PM' ((Req GET /api/dashboard -bearer $PM).Code -eq 200)

# --- Body size limit (1 MB): rejected with 413 in ApiResponse format
$big = '{"name":"' + ('x' * 1100000) + '"}'
$rb = Req POST /api/projects $big $A
Check 'body limit: 1.1 MB body -> 413 ApiResponse' ($rb.Code -eq 413 -and $rb.Body.success -eq $false -and $rb.Body.message -eq 'The request body is too large.') "$($rb.Code) $($rb.Body.message)"

# --- Global limit: 60 per minute per IP across all endpoints
$n = 0; $last = $null
while ($n -lt 80) { $n++; $last = Req GET /api/dashboard -bearer $A; if ($last.Code -eq 429) { break } }
Check 'global: reaches 429 within the 60/min budget (counting earlier calls)' ((Is429 $last) -and $n -le 60) "429 after $n more requests; Retry-After=$($last.RetryAfter)"
Check 'global: anonymous request also 429 (per IP, not per user)' ((Req GET /api/projects).Code -eq 429)
Check 'global: /health still 200 when the IP is limited' ((Req GET /health).Code -eq 200)
Check 'server log: rejections logged as warnings' (@(Select-String -Path $ServerLog -Pattern 'Rate limit exceeded for').Count -ge 4)

# --- Cleanup
Sql "DELETE FROM RefreshTokens WHERE UserId IN (SELECT Id FROM Users WHERE Email LIKE '%.$sfx@example.com'); DELETE FROM Users WHERE Email LIKE '%.$sfx@example.com'" | Out-Null
Check 'cleanup: test user removed' ((Sql "SELECT COUNT(*) FROM Users WHERE Email LIKE '%.$sfx@example.com'") -eq '0')
Complete-Suite
