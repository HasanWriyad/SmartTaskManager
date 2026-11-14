<#
.SYNOPSIS
    One-time developer setup for a fresh clone: checks prerequisites, sets user-secrets, creates the
    database. Safe to run again: existing secrets are kept unless -Force is given.

.DESCRIPTION
    1. Checks that a .NET SDK matching global.json is installed.
    2. Checks SQL Server LocalDB and creates/starts the MSSQLLocalDB instance if needed.
    3. Restores the local dotnet tools (dotnet-ef, Swashbuckle CLI).
    4. Sets user-secrets for the API project:
         Jwt:SigningKey      64 random bytes (Base64)
         SeedAdmin:Email     -AdminEmail (default admin@smarttask.local)
         SeedAdmin:Password  20 random characters; printed once at the end so you can sign in
         AiSettings:ApiKey   only when -AiApiKey is given; without it the offline AI fallback is used
    5. Optionally trusts the HTTPS development certificate (-TrustDevCert; shows a Windows prompt).
    6. Builds the API (which restores packages) and creates or migrates the database
       (dotnet ef database update), unless -SkipDatabase.

    Secret values are never printed, except a newly generated Admin password.
    Works in Windows PowerShell 5.1 and PowerShell 7.

.EXAMPLE
    ./tools/setup-dev.ps1
    ./tools/setup-dev.ps1 -TrustDevCert -AiApiKey <your Gemini API key>
#>
[CmdletBinding()]
param(
    [string]$AdminEmail = 'admin@smarttask.local',

    # Google Gemini API key. Optional: without it the API uses the offline fallback for AI suggestions.
    [string]$AiApiKey,

    # Run "dotnet dev-certs https --trust" (needed for the Secure refresh-token cookie over HTTPS).
    [switch]$TrustDevCert,

    # Regenerate the JWT key and Admin credentials even if they are already set.
    [switch]$Force,

    [switch]$SkipDatabase
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $root 'src/SmartTaskManager.Api'
$summary = New-Object System.Collections.Generic.List[string]
$generatedAdminPassword = $null

function Write-Step([string]$message) { Write-Host "==> $message" -ForegroundColor Cyan }

function Invoke-Native([string]$description, [scriptblock]$command) {
    $output = & $command 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | ForEach-Object { Write-Host "    $_" }
        throw "$description failed (exit code $LASTEXITCODE)."
    }
    return $output
}

function New-RandomBytes([int]$count) {
    $bytes = New-Object byte[] $count
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return , $bytes
}

# Meets the registration password rules: upper, lower, digit and symbol.
function New-AdminPassword([int]$length = 20) {
    $alphabet = [char[]]'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#%*-_+='
    do {
        $password = -join ((New-RandomBytes $length) | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
    } until ($password -cmatch '[A-Z]' -and $password -cmatch '[a-z]' -and $password -match '[0-9]' -and $password -match '[^A-Za-z0-9]')
    return $password
}

Push-Location $root
try {
    # --- 1. .NET SDK ------------------------------------------------------------------------
    Write-Step 'Checking the .NET SDK (global.json)'
    $required = (Get-Content (Join-Path $root 'global.json') -Raw | ConvertFrom-Json).sdk.version
    $sdkVersion = (& dotnet --version 2>&1 | Select-Object -Last 1)
    if ($LASTEXITCODE -ne 0 -or -not ("$sdkVersion" -match '^10\.0\.')) {
        throw "A .NET SDK $($required.Substring(0, 4)).x (>= $required) is required. Install it from https://dotnet.microsoft.com/download/dotnet/10.0"
    }
    $summary.Add(".NET SDK $sdkVersion")

    # --- 2. LocalDB -------------------------------------------------------------------------
    $customConnection = $env:ConnectionStrings__DefaultConnection
    if ($customConnection -and $customConnection -notmatch '\(localdb\)') {
        Write-Step 'Skipping the LocalDB check (ConnectionStrings__DefaultConnection points elsewhere)'
    }
    else {
        Write-Step 'Checking SQL Server LocalDB (MSSQLLocalDB)'
        if (-not (Get-Command sqllocaldb -ErrorAction SilentlyContinue)) {
            throw 'SqlLocalDB was not found. Install SQL Server Express LocalDB 2019 or newer (https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb).'
        }
        & sqllocaldb info MSSQLLocalDB *> $null
        if ($LASTEXITCODE -ne 0) {
            Invoke-Native 'Creating the MSSQLLocalDB instance' { sqllocaldb create MSSQLLocalDB -s } | Out-Null
            $summary.Add('LocalDB instance MSSQLLocalDB created and started')
        }
        else {
            Invoke-Native 'Starting MSSQLLocalDB' { sqllocaldb start MSSQLLocalDB } | Out-Null
            $summary.Add('LocalDB instance MSSQLLocalDB running')
        }
    }

    # --- 3. Local tools ---------------------------------------------------------------------
    Write-Step 'Restoring local dotnet tools'
    Invoke-Native 'dotnet tool restore' { dotnet tool restore } | Out-Null

    # --- 4. User secrets --------------------------------------------------------------------
    Write-Step 'Configuring user-secrets'
    $existing = @{}
    foreach ($line in (Invoke-Native 'Reading user-secrets' { dotnet user-secrets list --project $apiProject })) {
        if ("$line" -match '^(?<key>[^=]+?) = ') { $existing[$Matches['key']] = $true }
    }
    function Set-Secret([string]$key, [string]$value) {
        Invoke-Native "Setting $key" { dotnet user-secrets set $key $value --project $apiProject } | Out-Null
    }

    if ($Force -or -not $existing.ContainsKey('Jwt:SigningKey')) {
        Set-Secret 'Jwt:SigningKey' ([Convert]::ToBase64String((New-RandomBytes 64)))
        $summary.Add('Jwt:SigningKey        generated')
    }
    else { $summary.Add('Jwt:SigningKey        kept') }

    if ($Force -or -not $existing.ContainsKey('SeedAdmin:Email')) {
        Set-Secret 'SeedAdmin:Email' $AdminEmail
        $summary.Add("SeedAdmin:Email       set to $AdminEmail")
    }
    else { $summary.Add('SeedAdmin:Email       kept') }

    if ($Force -or -not $existing.ContainsKey('SeedAdmin:Password')) {
        $generatedAdminPassword = New-AdminPassword
        Set-Secret 'SeedAdmin:Password' $generatedAdminPassword
        $summary.Add('SeedAdmin:Password    generated (shown below)')
    }
    else { $summary.Add('SeedAdmin:Password    kept') }

    if ($AiApiKey) {
        Set-Secret 'AiSettings:ApiKey' $AiApiKey
        $summary.Add('AiSettings:ApiKey     set')
    }
    elseif ($existing.ContainsKey('AiSettings:ApiKey')) { $summary.Add('AiSettings:ApiKey     kept') }
    else { $summary.Add('AiSettings:ApiKey     not set: AI suggestions use the offline fallback (optional)') }

    # --- 5. HTTPS development certificate ---------------------------------------------------
    if ($TrustDevCert) {
        Write-Step 'Trusting the HTTPS development certificate'
        Invoke-Native 'dotnet dev-certs https --trust' { dotnet dev-certs https --trust } | Out-Null
        $summary.Add('HTTPS dev certificate trusted')
    }
    else {
        & dotnet dev-certs https --check --trust *> $null
        if ($LASTEXITCODE -eq 0) { $summary.Add('HTTPS dev certificate trusted') }
        else { $summary.Add('HTTPS dev certificate NOT trusted: run again with -TrustDevCert (needed for the refresh cookie)') }
    }

    # --- 6. Database ------------------------------------------------------------------------
    if ($SkipDatabase) {
        $summary.Add('Database              skipped (-SkipDatabase); it is also created on first run in Development')
    }
    else {
        # dotnet ef reads project metadata before building, which needs restored projects (obj/), so
        # restore and build first; a fresh clone has no obj folders yet.
        Write-Step 'Building the API (restores NuGet packages)'
        Invoke-Native 'dotnet build' { dotnet build src/SmartTaskManager.Api -nologo -v q } | Out-Null
        Write-Step 'Creating or migrating the database (dotnet ef database update)'
        Invoke-Native 'dotnet ef database update' {
            dotnet ef database update --no-build --project src/SmartTaskManager.Infrastructure --startup-project src/SmartTaskManager.Api
        } | Out-Null
        $summary.Add('Database              up to date')
    }

    # --- Summary ----------------------------------------------------------------------------
    Write-Host ''
    Write-Host 'Setup complete:' -ForegroundColor Green
    $summary | ForEach-Object { Write-Host "  $_" }
    if ($generatedAdminPassword) {
        Write-Host ''
        Write-Host 'Admin sign-in (shown once; also stored in user-secrets as SeedAdmin:Password):' -ForegroundColor Yellow
        Write-Host "  Email:    $(if ($existing.ContainsKey('SeedAdmin:Email') -and -not $Force) { '(unchanged, see dotnet user-secrets list)' } else { $AdminEmail })"
        Write-Host "  Password: $generatedAdminPassword"
        if ($existing.ContainsKey('SeedAdmin:Password')) {
            Write-Host '  Note: the Admin account is only created when no Admin exists yet. If one already exists,' -ForegroundColor Yellow
            Write-Host '        its password is unchanged; the new value only applies to a fresh database.' -ForegroundColor Yellow
        }
    }
    Write-Host ''
    Write-Host 'Start the API:'
    Write-Host '  dotnet run --project src/SmartTaskManager.Api --launch-profile https'
    Write-Host '  Swagger: https://localhost:7122/swagger   Health: https://localhost:7122/health'
}
finally {
    Pop-Location
}
