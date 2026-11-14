# /health must report 503 Unhealthy when the database disappears. Runs against an API started on a
# separate throwaway database (SmartTaskManager_HealthTest), which this script drops mid-run.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'health'
$db = $ctx.Database
function Get-Health { $o = (curl.exe -sk "$Base/health" -w "`n%{http_code}") -join "`n"; $l = $o -split "`n"; [pscustomobject]@{ Code = [int]$l[-1]; Text = ($l | Select-Object -SkipLast 1) -join "`n"; Body = (($l | Select-Object -SkipLast 1) -join "`n" | ConvertFrom-Json) } }

$before = sqlcmd -S '(localdb)\MSSQLLocalDB' -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$db'"
Check 'API created the throwaway database on startup' (($before | Where-Object { $_ -match '^\d+$' }) -eq '1')
$h = Get-Health
Check 'before: 200 Healthy' ($h.Code -eq 200 -and $h.Body.data.status -eq 'Healthy')
sqlcmd -S '(localdb)\MSSQLLocalDB' -d master -I -b -Q "ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$db];" | Out-Null
$h = Get-Health
Check 'after dropping the DB: 503, success=false, Unhealthy' ($h.Code -eq 503 -and $h.Body.success -eq $false -and $h.Body.data.status -eq 'Unhealthy') "$($h.Code) $($h.Body.data.status) '$($h.Body.message)'"
Check 'database check reported Unhealthy' (@($h.Body.data.checks | Where-Object { $_.name -eq 'database' -and $_.status -eq 'Unhealthy' }).Count -eq 1)
Check 'no exception details in the response' ($h.Text -notmatch '(?i)exception|sqlclient|login failed|cannot open database|stack')
$exists = sqlcmd -S '(localdb)\MSSQLLocalDB' -d master -h -1 -W -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM sys.databases WHERE name = '$db'"
Check 'throwaway database is gone' (($exists | Where-Object { $_ -match '^\d+$' }) -eq '0')
Complete-Suite
