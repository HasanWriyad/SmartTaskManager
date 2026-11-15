<#
.SYNOPSIS
    Runs the API check suites against a freshly built API and a throwaway database.

.DESCRIPTION
    Builds the API into a temporary folder (never the project's bin, so a running instance is not
    disturbed), creates a throwaway database per run, starts the API once per configuration group on
    its own port, runs the suites, stops only the processes it started, and drops the database.
    Exits with code 1 when any check fails.

.EXAMPLE
    ./tools/api-checks/Run-ApiChecks.ps1
    ./tools/api-checks/Run-ApiChecks.ps1 -Suite auth, projects
    ./tools/api-checks/Run-ApiChecks.ps1 -Live          # also calls the real AI provider (uses API quota)
#>
[CmdletBinding()]
param(
    # Suites to run (default: all except ai-live). See README.md for the list.
    [string[]]$Suite,

    # Also run ai-live: three requests to the real AI provider configured in user-secrets.
    [switch]$Live,

    [int]$Port = 7300,

    # Keep the throwaway database(s) after the run, for debugging.
    [switch]$KeepDatabase
)

$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$root = (Resolve-Path (Join-Path $here '..\..')).Path
Import-Module (Join-Path $here 'ApiChecks.psm1') -Force

$unlimited = @{ 'RateLimiting__GlobalPerMinute' = '100000'; 'RateLimiting__AuthPerMinute' = '100000'; 'RateLimiting__AiPerMinute' = '100000' }
$noAiKey = @{ 'AiSettings__ApiKey' = ' ' } # a blank value overrides user-secrets: offline fallback
$fakeAi = @{ 'AiSettings__Endpoint' = 'http://localhost:7399/v1beta/openai/'; 'AiSettings__ApiKey' = 'fake-test-token'; 'AiSettings__TimeoutSeconds' = '3' }
function Merge([hashtable[]]$parts) { $m = @{}; foreach ($p in $parts) { foreach ($k in $p.Keys) { $m[$k] = $p[$k] } }; $m }

# Each group runs against one API process with its own configuration.
$groups = @(
    [pscustomobject]@{ Name = 'core'; Suites = @('auth', 'users', 'projects', 'tasks', 'lists', 'dashboard', 'ai-fallback'); Env = (Merge @($unlimited, $noAiKey)); SqlLog = $true; OwnDatabase = $false }
    [pscustomobject]@{ Name = 'ai-provider'; Suites = @('ai-provider'); Env = (Merge @($unlimited, $fakeAi)); SqlLog = $false; OwnDatabase = $false }
    [pscustomobject]@{ Name = 'ai-no-reasoning-effort'; Suites = @('ai-no-reasoning-effort'); Env = (Merge @($unlimited, $fakeAi, @{ 'AiSettings__ReasoningEffort' = ' ' })); SqlLog = $false; OwnDatabase = $false }
    [pscustomobject]@{ Name = 'rate-limits'; Suites = @('rate-limits'); Env = (Merge @(@{ 'RateLimiting__GlobalPerMinute' = '60' }, $noAiKey)); SqlLog = $false; OwnDatabase = $false }
    [pscustomobject]@{ Name = 'health'; Suites = @('health'); Env = $noAiKey; SqlLog = $false; OwnDatabase = $true } # the suite drops its database
    [pscustomobject]@{ Name = 'ai-live'; Suites = @('ai-live'); Env = $unlimited; SqlLog = $false; OwnDatabase = $false }
)
$allSuites = $groups | ForEach-Object { $_.Suites }
if ($Suite) {
    $unknown = $Suite | Where-Object { $_ -notin $allSuites }
    if ($unknown) { throw "Unknown suite(s): $($unknown -join ', '). Available: $($allSuites -join ', ')" }
}
$selected = if ($Suite) { $Suite } else { $allSuites | Where-Object { $_ -ne 'ai-live' -or $Live } }
if ($Live -and $selected -notcontains 'ai-live') { $selected += 'ai-live' }

# --- Prerequisites -------------------------------------------------------------------------
foreach ($tool in 'curl.exe', 'sqlcmd', 'dotnet') {
    if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "$tool was not found on PATH (see README.md)." }
}
$secrets = dotnet user-secrets list --project (Join-Path $root 'src/SmartTaskManager.Api')
foreach ($key in 'Jwt:SigningKey', 'SeedAdmin:Email', 'SeedAdmin:Password') {
    if (-not ($secrets | Where-Object { $_ -like "$key = *" })) { throw "user-secret $key is missing. Run tools/setup-dev.ps1 first." }
}
if ($selected -contains 'ai-live' -and -not ($secrets | Where-Object { $_ -like 'AiSettings:ApiKey = *' })) {
    throw 'ai-live needs a real AiSettings:ApiKey in user-secrets (tools/setup-dev.ps1 -AiApiKey ...).'
}

# --- Build ---------------------------------------------------------------------------------
$work = Join-Path ([IO.Path]::GetTempPath()) 'stm-api-checks'
$out = Join-Path $work 'api'
Write-Host "==> Building the API into $out"
$build = dotnet build (Join-Path $root 'src/SmartTaskManager.Api') -c Debug -o $out -nologo -v q 2>&1
if ($LASTEXITCODE -ne 0) { $build | Select-String 'error' | ForEach-Object { Write-Host $_ }; throw 'Build failed.' }

$runId = -join ((65..90) | Get-Random -Count 6 | ForEach-Object { [char]$_ })
$sharedDb = "SmartTaskManager_ApiChecks_$runId"
$createdDbs = New-Object System.Collections.Generic.List[string]

function Start-Api([hashtable]$envVars, [string]$database, [switch]$SqlLog) {
    $log = Join-Path $work "server-$(Get-Date -Format 'HHmmss')-$([guid]::NewGuid().ToString('N').Substring(0, 6)).log"
    $vars = @{
        'ASPNETCORE_ENVIRONMENT' = 'Development'
        'ASPNETCORE_URLS' = "https://localhost:$Port"
        'SeedDemo__Enabled' = 'false' # checks expect an empty database
        'ConnectionStrings__DefaultConnection' = "Server=(localdb)\MSSQLLocalDB;Database=$database;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5"
    }
    if ($SqlLog) {
        $vars['Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command'] = 'Information'
        $vars['Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Transaction'] = 'Debug'
    }
    foreach ($k in $envVars.Keys) { $vars[$k] = $envVars[$k] }
    foreach ($k in $vars.Keys) { [Environment]::SetEnvironmentVariable($k, $vars[$k], 'Process') }
    try {
        $process = Start-Process dotnet -ArgumentList "`"$out\SmartTaskManager.Api.dll`"" -WorkingDirectory $out `
            -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru -WindowStyle Hidden
    }
    finally {
        foreach ($k in $vars.Keys) { [Environment]::SetEnvironmentVariable($k, $null, 'Process') }
    }
    $deadline = (Get-Date).AddSeconds(90)
    while ((Get-Date) -lt $deadline -and -not (Select-String -Path $log -Pattern 'Application started|FTL|Hosting failed' -Quiet -ErrorAction SilentlyContinue)) {
        Start-Sleep -Milliseconds 500
    }
    if (-not (Select-String -Path $log -Pattern 'Application started' -Quiet -ErrorAction SilentlyContinue)) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        Get-Content $log -Tail 20 | ForEach-Object { Write-Host "    $_" }
        throw "The API did not start (log: $log)."
    }
    [pscustomobject]@{ Process = $process; Log = $log }
}

function Remove-Database([string]$name) {
    sqlcmd -S '(localdb)\MSSQLLocalDB' -d master -b -Q "IF DB_ID('$name') IS NOT NULL BEGIN ALTER DATABASE [$name] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$name]; END" | Out-Null
}

$exitCode = 0
Push-Location $root
try {
    foreach ($group in $groups) {
        $suites = @($group.Suites | Where-Object { $_ -in $selected })
        if ($suites.Count -eq 0) { continue }
        $database = if ($group.OwnDatabase) { "${sharedDb}_$($group.Name)" } else { $sharedDb }
        if (-not $createdDbs.Contains($database)) { $createdDbs.Add($database) }
        Write-Host "==> Group '$($group.Name)': starting the API on port $Port (database $database)"
        $api = Start-Api $group.Env $database -SqlLog:$group.SqlLog
        try {
            Set-ApiChecksContext -BaseUrl "https://localhost:$Port" -Database $database -ServerLog $api.Log -RepoRoot $root
            foreach ($name in $suites) {
                Write-Host "===== $name"
                try { & (Join-Path $here "suites\$name.ps1") }
                catch { Complete-Suite -ErrorMessage $_.Exception.Message }
            }
        }
        finally {
            Stop-Process -Id $api.Process.Id -Force -ErrorAction SilentlyContinue
            Start-Sleep -Milliseconds 500
        }
    }
}
finally {
    Pop-Location
    if (-not $KeepDatabase) { foreach ($db in $createdDbs) { Remove-Database $db } }
    elseif ($createdDbs.Count -gt 0) { Write-Host "Kept database(s): $($createdDbs -join ', ')" }
}

$results = Get-ApiChecksResults
$total = ($results | Measure-Object Total -Sum).Sum
$failed = ($results | Measure-Object Failed -Sum).Sum
Write-Host ''
Write-Host 'Summary:'
$results | ForEach-Object { Write-Host ('  {0,-24} {1,4}/{2,-4} {3}' -f $_.Name, ($_.Total - $_.Failed), $_.Total, $(if ($_.Failed) { 'FAILED' } else { 'ok' })) }
Write-Host ("  {0,-24} {1,4}/{2,-4}" -f 'total', ($total - $failed), $total)
if ($failed -gt 0) { $exitCode = 1 }
exit $exitCode
