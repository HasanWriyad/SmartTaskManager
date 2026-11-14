# End-to-end test of /api/auth against a running API (https profile). Mirrors what Swagger UI sends.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'auth'
$jarA = Join-Path $tmp 'jarA.txt'; $jarB = Join-Path $tmp 'jarB.txt'; Remove-Item "$tmp\*" -Force -ErrorAction SilentlyContinue

function Cookie([string]$jar) { (Get-Content $jar -ErrorAction SilentlyContinue | Where-Object { $_ -match 'stm_refresh' } | ForEach-Object { ($_ -split "`t")[-1] }) | Select-Object -Last 1 }
function SetCookie([string]$jar, [string]$value) { $lines = Get-Content $jar | ForEach-Object { if ($_ -match 'stm_refresh') { $p = $_ -split "`t"; $p[-1] = $value; $p -join "`t" } else { $_ } }; Set-Content $jar $lines -Encoding ascii }

$email = "member$(Get-Random -Maximum 99999)@example.com"
$pw = 'Str0ng!Passw0rd'

# --- Swagger document
$sw = curl.exe -sk "$Base/swagger/v1/swagger.json" | ConvertFrom-Json
$ops = 'register', 'login', 'refresh', 'logout' | ForEach-Object { $sw.paths."/api/auth/$_".post }
Check 'swagger lists register/login/refresh/logout' (@($ops | Where-Object { $_ }).Count -eq 4)
Check 'auth endpoints are anonymous in swagger (no lock)' (@($ops | Where-Object { $_ }).Count -eq 4 -and -not ($ops | Where-Object { $_.security }))

# --- Register
$r = Req POST /api/auth/register @{ email = 'not-an-email'; fullName = ''; password = 'weak' }
Check 'register invalid input -> 400 with field errors' ($r.Code -eq 400 -and $r.Body.errors.email -and $r.Body.errors.fullName -and $r.Body.errors.password) ("keys: " + ($r.Body.errors.PSObject.Properties.Name -join ','))
"       password errors: " + ($r.Body.errors.password -join ' | ')
$r = Req POST /api/auth/register @{ email = $email; fullName = 'Test Member'; password = $pw; role = 'Admin' }
Check 'register valid (role=Admin in body ignored) -> 201 TeamMember' ($r.Code -eq 201 -and $r.Body.data.role -eq 'TeamMember') "role=$($r.Body.data.role)"
$r = Req POST /api/auth/register @{ email = $email.ToUpper(); fullName = 'Dup'; password = $pw }
Check 'register duplicate email (different case) -> 409' ($r.Code -eq 409) $r.Body.message
$r = Req POST /api/auth/register @{}
Check 'register empty JSON object -> 400 with all field errors' ($r.Code -eq 400 -and $r.Body.errors.email -and $r.Body.errors.password)
$o = (curl.exe -sk -X POST "$Base/api/auth/register" -H 'Content-Type: application/json' -w "`n%{http_code}") -split "`n"
Check 'register missing body -> 400 ApiResponse (key "body")' ($o[-1] -eq '400' -and ($o[0] | ConvertFrom-Json).errors.body) $o[0]
$o = (curl.exe -sk -X POST "$Base/api/auth/register" -H 'Content-Type: text/plain' --data-raw 'hi' -w "`n%{http_code}") -split "`n"
Check 'register text/plain -> 415 as ApiResponse (not ProblemDetails)' ($o[-1] -eq '415' -and ($o[0] | ConvertFrom-Json).success -eq $false) $o[0]

# --- Login
$r = Req POST /api/auth/login @{ email = $email; password = 'Wrong!Passw0rd' }
Check 'login wrong password -> 401' ($r.Code -eq 401) $r.Body.message
$r = Req POST /api/auth/login @{ email = 'nobody@example.com'; password = $pw }
Check 'login unknown email -> 401, same message' ($r.Code -eq 401 -and $r.Body.message -eq 'Invalid email or password.')
$r = Req POST /api/auth/login @{ email = ''; password = '' }
Check 'login empty fields -> 400' ($r.Code -eq 400)
$r = Req POST /api/auth/login @{ email = "  $($email.ToUpper()) "; password = $pw } -jar $jarA
$setCookie = ($r.HeaderLines | Where-Object { $_ -match '^set-cookie:' }) -join ''
Check 'login ok -> 200 with access token in body' ($r.Code -eq 200 -and $r.Body.data.accessToken -and $r.Body.data.user.email -eq $email)
Check 'refresh token NOT in response body' ($r.Code -eq 200 -and $r.Body.data.PSObject.Properties.Name -notcontains 'refreshToken' -and $r.Text -notmatch '"refreshToken"')
Check 'cookie: httponly; secure; samesite=strict; path=/api/auth' ($setCookie -match 'httponly' -and $setCookie -match 'secure' -and $setCookie -match 'samesite=strict' -and $setCookie -match 'path=/api/auth') ($setCookie.Trim() -replace 'stm_refresh=[^;]+', 'stm_refresh=<redacted>')
$access1 = $r.Body.data.accessToken
$parts = $access1.Split('.'); $p = $parts[1].Replace('-', '+').Replace('_', '/'); $p += '=' * ((4 - $p.Length % 4) % 4)
$claims = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($p)) | ConvertFrom-Json
Check 'access token claims: sub/role/iss/aud, ~15 min lifetime' ($claims.sub -and $claims.role -eq 'TeamMember' -and $claims.iss -eq 'SmartTaskManager.Api' -and [math]::Abs(($claims.exp - $claims.iat) - 900) -le 1) "role=$($claims.role) ttl=$($claims.exp - $claims.iat)s"
Check 'access token accepted by API (unknown route -> 404 not 401)' ((Req GET /api/nope -bearer $access1).Code -eq 404)

# --- Refresh + rotation
$cookie1 = Cookie $jarA
$r = Req POST /api/auth/refresh -jar $jarA
$cookie2 = Cookie $jarA
Check 'refresh with cookie -> 200 new access token' ($r.Code -eq 200 -and $r.Body.data.accessToken -and $r.Body.data.accessToken -ne $access1)
Check 'refresh rotated the cookie value' ($cookie2 -and $cookie2 -ne $cookie1)
$r = Req POST /api/auth/refresh -jar $jarA
$cookie3 = Cookie $jarA
Check 'second refresh with rotated cookie -> 200' ($r.Code -eq 200 -and $cookie3 -ne $cookie2)
$r = Req POST /api/auth/refresh
Check 'refresh without cookie -> 401' ($r.Code -eq 401) $r.Body.message

# --- Reuse detection: replay cookie1 (already rotated) -> whole family revoked, so cookie3 dies too
Copy-Item $jarA $jarB; SetCookie $jarB $cookie1
$r = Req POST /api/auth/refresh -jar $jarB
Check 'replay of rotated (revoked) token -> 401' ($r.Code -eq 401)
$r = Req POST /api/auth/refresh -jar $jarA
Check 'latest token of that family now rejected (chain revoked)' ($r.Code -eq 401)

# --- Concurrent refresh with the same token: exactly one may win, then the family is revoked
Remove-Item $jarA; $null = Req POST /api/auth/login @{ email = $email; password = $pw } -jar $jarA
$c = Cookie $jarA
$race = curl.exe -sk --parallel --parallel-immediate -o NUL -o NUL -w "%{http_code}`n" -X POST -b "stm_refresh=$c" "$Base/api/auth/refresh" "$Base/api/auth/refresh"
$sorted = ($race | Sort-Object) -join ','
Check 'two parallel refreshes with one token -> one 200, one 401' ($c -and ($sorted -eq '200,401' -or $sorted -eq '401,401')) "codes: $sorted"
Copy-Item $jarA $jarB -Force
$after = (Req POST /api/auth/refresh -jar $jarB).Code
Check 'after the race the family is revoked (original token dead)' ($after -eq 401)

# --- Logout
Remove-Item $jarA; $null = Req POST /api/auth/login @{ email = $email; password = $pw } -jar $jarA
$beforeLogout = Cookie $jarA
$r = Req POST /api/auth/logout -jar $jarA
$clear = ($r.HeaderLines | Where-Object { $_ -match '^set-cookie:' }) -join ''
Check 'logout -> 200 and cookie cleared (expired, same path)' ($r.Code -eq 200 -and $clear -match 'expires=Thu, 01 Jan 1970' -and $clear -match 'path=/api/auth')
SetCookie $jarB $beforeLogout
Check 'refresh with the logged-out token -> 401' ((Req POST /api/auth/refresh -jar $jarB).Code -eq 401)
Check 'logout again without cookie -> 200 (idempotent)' ((Req POST /api/auth/logout).Code -eq 200)

# --- Inactive user
Remove-Item $jarA; $null = Req POST /api/auth/login @{ email = $email; password = $pw } -jar $jarA
sqlcmd -S "(localdb)\MSSQLLocalDB" -d $ctx.Database -Q "SET NOCOUNT ON; UPDATE Users SET IsActive = 0 WHERE Email = '$email'" | Out-Null
$r = Req POST /api/auth/login @{ email = $email; password = $pw }
Check 'inactive user login -> 401 deactivated' ($r.Code -eq 401 -and $r.Body.message -match 'deactivated') $r.Body.message
$r = Req POST /api/auth/refresh -jar $jarA
Check 'inactive user refresh with live cookie -> 401' ($r.Code -eq 401 -and $r.Body.message -match 'deactivated')
$r = Req POST /api/auth/login @{ email = $email; password = 'Wrong!Passw0rd' }
Check 'inactive user + wrong password -> generic message (no status leak)' ($r.Body.message -eq 'Invalid email or password.')

# --- Error responses keep the ApiResponse shape
Check 'errors use ApiResponse shape with traceId' ($r.Body.success -eq $false -and $r.Body.traceId)

# --- Database: hashes only, family/rotation bookkeeping
$db = sqlcmd -S "(localdb)\MSSQLLocalDB" -d $ctx.Database -h -1 -W -s '|' -Q "SET NOCOUNT ON; SELECT COUNT(*), COUNT(DISTINCT t.FamilyId), SUM(CASE WHEN t.RevokedAt IS NULL THEN 1 ELSE 0 END), SUM(CASE WHEN t.ReplacedByTokenId IS NOT NULL THEN 1 ELSE 0 END), MIN(LEN(t.TokenHash)) FROM RefreshTokens t JOIN Users u ON u.Id = t.UserId WHERE u.Email = '$email'"
$f = ($db | Where-Object { $_ -match '\|' }) -split '\|'
Check 'DB: tokens stored as 64-char hashes, all revoked at the end' ([int]$f[0] -gt 0 -and [int]$f[2] -eq 0 -and [int]$f[4] -eq 64) "tokens=$($f[0]) families=$($f[1]) active=$($f[2]) rotated=$($f[3]) hashLen=$($f[4])"
$rawInDb = sqlcmd -S "(localdb)\MSSQLLocalDB" -d $ctx.Database -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM RefreshTokens WHERE TokenHash IN ('$cookie1','$cookie2','$cookie3')"
Check 'DB: raw cookie values are not stored' ($cookie1 -and $cookie3 -and (($rawInDb | Where-Object { $_ -match '^\d+$' }) -as [int]) -eq 0)
$hash1 = ([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($cookie1)))).Replace('-', '')
$hashed = sqlcmd -S "(localdb)\MSSQLLocalDB" -d $ctx.Database -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM RefreshTokens WHERE TokenHash = '$hash1'"
Check 'DB: SHA-256 of the cookie value is what is stored' ((($hashed | Where-Object { $_ -match '^\d+$' }) -as [int]) -eq 1)

# Cleanup: remove the test user and its tokens
sqlcmd -S "(localdb)\MSSQLLocalDB" -d $ctx.Database -Q "SET NOCOUNT ON; DELETE t FROM RefreshTokens t JOIN Users u ON u.Id = t.UserId WHERE u.Email = '$email'; DELETE FROM Users WHERE Email = '$email'" | Out-Null
Complete-Suite
@{ Cookies = @($cookie1, $cookie2, $cookie3) } | ConvertTo-Json -Compress | Set-Content (Join-Path $tmp 'cookies.json')
