# POST /api/ai/improve-description with no AI token configured: the offline fallback answers.
$ctx = Get-ApiChecksContext
$Base = $ctx.BaseUrl
$ServerLog = $ctx.ServerLog
$tmp = Start-Suite 'ai-fallback'

$secrets = dotnet user-secrets list --project src/SmartTaskManager.Api
if (-not (Select-String -Path $ServerLog -Pattern 'uses the offline fallback' -Quiet)) { Check 'fallback active (the runner blanks AiSettings:ApiKey)' $false; Complete-Suite; return }
$A = (Req POST /api/auth/login @{ email = (($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', ''); password = (($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', '') }).Body.data.accessToken

Check 'startup log: fallback active (no token configured)' (Select-String -Path $ServerLog -Pattern 'uses the offline fallback' -Quiet)
$r = Req POST /api/ai/improve-description @{ description = "  fix   the login   page  " } $A
$text = $r.Body.data.improvedDescription
Check 'fallback: 200, source fallback, no model' ($r.Code -eq 200 -and $r.Body.data.source -eq 'fallback' -and $null -eq $r.Body.data.model)
Check 'fallback: tidied (whitespace, capital, period) + steps' ($text.StartsWith("Fix the login page.`n`nSteps:`n1. ") -and ($text -split "`n" | Where-Object { $_ -match '^\d\. ' }).Count -eq 5) ($text -split "`n")[0]
Check 'fallback: validation still applies (2001 chars -> 400)' ((Req POST /api/ai/improve-description @{ description = ('x' * 2001) } $A).Code -eq 400)
Complete-Suite
