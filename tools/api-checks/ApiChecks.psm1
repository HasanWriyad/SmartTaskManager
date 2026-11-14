# Shared helpers for the API check suites (see README.md). Works in Windows PowerShell 5.1 and PowerShell 7.
# The runner (Run-ApiChecks.ps1) sets the context; suites call Req, Sql, Login and Check.

$script:Context = $null
$script:CurrentSuite = $null
$script:Results = New-Object System.Collections.Generic.List[object]

function Set-ApiChecksContext {
    param(
        [Parameter(Mandatory)][string]$BaseUrl,
        [Parameter(Mandatory)][string]$Database,
        [Parameter(Mandatory)][string]$ServerLog,
        [Parameter(Mandatory)][string]$RepoRoot,
        [string]$SqlServer = '(localdb)\MSSQLLocalDB'
    )
    $script:Context = [pscustomobject]@{
        BaseUrl = $BaseUrl; Database = $Database; ServerLog = $ServerLog; RepoRoot = $RepoRoot; SqlServer = $SqlServer
        FakeAiServer = Join-Path $PSScriptRoot 'fake-openai.ps1'
    }
}

function Get-ApiChecksContext {
    if (-not $script:Context) { throw 'Run the suites through Run-ApiChecks.ps1 (no context set).' }
    $script:Context
}

function Start-Suite([Parameter(Mandatory)][string]$Name) {
    $script:CurrentSuite = [pscustomobject]@{ Name = $Name; Total = 0; Failed = 0; Error = $null }
    $temp = Join-Path ([IO.Path]::GetTempPath()) "stm-api-checks\$Name"
    New-Item -ItemType Directory -Force $temp | Out-Null
    Remove-Item (Join-Path $temp '*') -Recurse -Force -ErrorAction SilentlyContinue
    $temp
}

function Complete-Suite([string]$ErrorMessage) {
    $suite = $script:CurrentSuite
    if (-not $suite) { return }
    if ($ErrorMessage) { $suite.Error = $ErrorMessage; $suite.Failed++; $suite.Total++ ; "ERROR $($suite.Name): $ErrorMessage" }
    "--- $($suite.Name): $($suite.Total - $suite.Failed)/$($suite.Total) passed"
    $script:Results.Add($suite)
    $script:CurrentSuite = $null
}

function Get-ApiChecksResults { , $script:Results }

# Records one check and prints a PASS/FAIL line.
function Check([string]$Name, [bool]$Ok, [string]$Detail = '') {
    $script:CurrentSuite.Total++
    if (-not $Ok) { $script:CurrentSuite.Failed++ }
    '{0} {1,-70} {2}' -f $(if ($Ok) { 'PASS' } else { 'FAIL' }), $Name, $Detail
}

# HTTP request via curl.exe. Body: hashtable/object (sent as JSON) or a raw string.
# Returns Code, Text (body), Body (parsed JSON or $null), HeaderLines, RetryAfter.
function Req {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Path,
        $Body = $null,
        [string]$Bearer = $null,
        [string]$Jar = $null
    )
    $ctx = Get-ApiChecksContext
    $dir = Join-Path ([IO.Path]::GetTempPath()) 'stm-api-checks\_requests'
    New-Item -ItemType Directory -Force $dir | Out-Null
    $id = [guid]::NewGuid().ToString('N')
    $headerFile = Join-Path $dir "$id.headers"
    $curlArgs = @('-sk', '-X', $Method, "$($ctx.BaseUrl)$Path", '-D', $headerFile, '-w', "`n%{http_code}")
    $bodyFile = $null
    if ($null -ne $Body) {
        $bodyFile = Join-Path $dir "$id.json"
        $json = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Compress -Depth 10 }
        [IO.File]::WriteAllText($bodyFile, $json, (New-Object System.Text.UTF8Encoding $false))
        $curlArgs += @('-H', 'Content-Type: application/json', '--data-binary', "@$bodyFile")
    }
    if ($Bearer) { $curlArgs += @('-H', "Authorization: Bearer $Bearer") }
    if ($Jar) { $curlArgs += @('-b', $Jar, '-c', $Jar) }
    try {
        $out = (& curl.exe @curlArgs) -join "`n"
        $lines = $out -split "`n"
        $text = ($lines | Select-Object -SkipLast 1) -join "`n"
        $headerLines = @(Get-Content $headerFile -ErrorAction SilentlyContinue | ForEach-Object { $_.TrimEnd() } | Where-Object { $_ })
        $retry = $headerLines | Select-String '^Retry-After:\s*(\d+)' | Select-Object -First 1
        [pscustomobject]@{
            Code        = [int]$lines[-1]
            Text        = $text
            Body        = ($text | ConvertFrom-Json -ErrorAction SilentlyContinue)
            HeaderLines = $headerLines
            RetryAfter  = $(if ($retry) { $retry.Matches[0].Groups[1].Value } else { $null })
        }
    }
    finally {
        # $bodyFile is $null for requests without a body; a null path is a binding error, not a missing file.
        foreach ($file in @($headerFile, $bodyFile) | Where-Object { $_ }) { Remove-Item $file -ErrorAction SilentlyContinue }
    }
}

# Query the test database; returns non-empty output lines. -I: QUOTED_IDENTIFIER ON (needed for
# writes to tables with filtered indexes, as SqlClient sets it).
function Sql([Parameter(Mandatory)][string]$Query, [string]$Database) {
    $ctx = Get-ApiChecksContext
    $db = if ($Database) { $Database } else { $ctx.Database }
    @(sqlcmd -S $ctx.SqlServer -d $db -I -h -1 -W -s '|' -b -Q "SET NOCOUNT ON; $Query") | Where-Object { $_ -and $_ -notmatch '^\s*$' }
}

# Runs a batch that must start the batch (e.g. CREATE TRIGGER); throws on failure.
function SqlDdl([Parameter(Mandatory)][string]$Query) {
    $ctx = Get-ApiChecksContext
    sqlcmd -S $ctx.SqlServer -d $ctx.Database -I -b -Q $Query | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "DDL failed: $Query" }
}

# Signs in and returns the access token (cookies go to -Jar when given).
function Login([string]$Email, [string]$Password, [string]$Jar = $null) {
    (Req POST /api/auth/login @{ email = $Email; password = $Password } -Jar $Jar).Body.data.accessToken
}

# The seeded Admin's credentials from the API project's user-secrets (cached).
function Get-SeedAdmin {
    if (-not $script:SeedAdmin) {
        $ctx = Get-ApiChecksContext
        $secrets = dotnet user-secrets list --project (Join-Path $ctx.RepoRoot 'src/SmartTaskManager.Api')
        $script:SeedAdmin = [pscustomobject]@{
            Email    = (($secrets | Where-Object { $_ -like 'SeedAdmin:Email = *' }) -replace '^SeedAdmin:Email = ', '')
            Password = (($secrets | Where-Object { $_ -like 'SeedAdmin:Password = *' }) -replace '^SeedAdmin:Password = ', '')
        }
    }
    $script:SeedAdmin
}

Export-ModuleMember -Function Set-ApiChecksContext, Get-ApiChecksContext, Start-Suite, Complete-Suite, Get-ApiChecksResults, Check, Req, Sql, SqlDdl, Login, Get-SeedAdmin
