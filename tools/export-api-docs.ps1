<#
.SYNOPSIS
    Regenerates the API deliverables in docs/:
      - database.sql                          idempotent EF Core migration script
      - swagger.json                          OpenAPI document (Swashbuckle CLI, no running server needed)
      - SmartTaskManager.postman_collection.json  Postman v2.1 collection generated from swagger.json

.DESCRIPTION
    Builds the API in Release (so it does not collide with a running Debug instance), then runs the
    local dotnet tools pinned in dotnet-tools.json. Works in Windows PowerShell 5.1 and PowerShell 7.
    Swagger generation loads the app's services in the Development environment, so the Development
    settings (connection string) must be present; nothing is started and the database is not touched.

.EXAMPLE
    ./tools/export-api-docs.ps1
    ./tools/export-api-docs.ps1 -BaseUrl https://localhost:7122
#>
[CmdletBinding()]
param(
    # Default value of the {{baseUrl}} collection variable (the API's https launch profile).
    [string]$BaseUrl = 'https://localhost:7122'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$docs = Join-Path $root 'docs'

function Invoke-Native([string]$description, [scriptblock]$command) {
    Write-Host "==> $description"
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$description failed (exit code $LASTEXITCODE)" }
}

# ---------------------------------------------------------------------------------------------
# OpenAPI -> Postman v2.1
# ---------------------------------------------------------------------------------------------

# Friendlier example values for well-known property names; everything else comes from the schema.
$script:ExampleOverrides = @{
    email       = 'user@example.com'
    password    = 'Str0ng!Passw0rd'
    fullName    = 'Jane Doe'
    name        = 'Website redesign'
    title       = 'Write the release notes'
    description = 'Summarize the changes in this release for customers.'
    dueDate     = '2026-12-31'
    assigneeId  = '{{userId}}'
    userId      = '{{userId}}'
    # Query parameters (the requests include them disabled; enable the ones you need)
    page        = 1
    pageSize    = 20
    search      = ''
    sortBy      = ''
    dueFrom     = '2026-12-01'
    dueTo       = '2026-12-31'
}

function Get-SchemaExample($schema, $components, [string]$propertyName, [int]$depth) {
    if ($null -eq $schema -or $depth -gt 8) { return $null }
    if ($propertyName -and $script:ExampleOverrides.ContainsKey($propertyName)) { return $script:ExampleOverrides[$propertyName] }
    if ($schema.'$ref') {
        $name = $schema.'$ref' -replace '^#/components/schemas/', ''
        return Get-SchemaExample $components.$name $components $propertyName ($depth + 1)
    }
    foreach ($combinator in 'allOf', 'oneOf', 'anyOf') {
        if ($schema.$combinator) { return Get-SchemaExample @($schema.$combinator)[0] $components $propertyName ($depth + 1) }
    }
    if ($schema.enum) { return @($schema.enum)[0] }

    # OpenAPI 3.1 allows "type": ["string", "null"]; use the first non-null type.
    $type = @($schema.type) | Where-Object { $_ -and $_ -ne 'null' } | Select-Object -First 1
    if (-not $type -and $schema.properties) { $type = 'object' }
    switch ($type) {
        'object' {
            $obj = [ordered]@{}
            if ($schema.properties) {
                foreach ($p in $schema.properties.PSObject.Properties) {
                    $obj[$p.Name] = Get-SchemaExample $p.Value $components $p.Name ($depth + 1)
                }
            }
            return $obj
        }
        'array' {
            $list = New-Object System.Collections.Generic.List[object]
            $item = Get-SchemaExample $schema.items $components $null ($depth + 1)
            if ($null -ne $item) { $list.Add($item) }
            return , $list
        }
        'integer' { return 1 }
        'number' { return 1 }
        'boolean' { return $true }
        'string' {
            switch ($schema.format) {
                'uuid' { return '00000000-0000-0000-0000-000000000000' }
                'date' { return '2026-12-31' }
                'date-time' { return '2026-12-31T09:00:00Z' }
                default { return 'string' }
            }
        }
        default { return $null }
    }
}

# Maps a path parameter to the collection variable that holds its value.
function Get-PathVariableName([string]$path, [string]$parameter) {
    if ($parameter -ne 'id') { return $parameter }
    if ($path -match '^/api/projects/\{id') { return 'projectId' }
    if ($path -match '^/api/users/\{id') { return 'userId' }
    return 'id'
}

# Postman test scripts that store values for the following requests.
function Get-TestScript([string]$method, [string]$path) {
    $set = $null
    switch ("$method $path") {
        'POST /api/auth/login' { $set = 'pm.collectionVariables.set("accessToken", pm.response.json().data.accessToken);' }
        'POST /api/auth/refresh' { $set = 'pm.collectionVariables.set("accessToken", pm.response.json().data.accessToken);' }
        'POST /api/auth/register' { $set = 'pm.collectionVariables.set("userId", pm.response.json().data.id);' }
        'POST /api/projects' { $set = 'pm.collectionVariables.set("projectId", pm.response.json().data.id);' }
        'POST /api/projects/{projectId}/tasks' { $set = 'pm.collectionVariables.set("taskId", pm.response.json().data.id);' }
    }
    if (-not $set) { return $null }
    $exec = New-Object System.Collections.Generic.List[string]
    $exec.Add('if (pm.response.code >= 200 && pm.response.code < 300) {')
    $exec.Add("    $set")
    $exec.Add('}')
    $events = New-Object System.Collections.Generic.List[object]
    $events.Add([ordered]@{ listen = 'test'; script = [ordered]@{ type = 'text/javascript'; exec = $exec } })
    return , $events
}

function New-PostmanRequest([string]$method, [string]$path, $operation, $components) {
    $pathNoConstraints = $path -replace '\{(\w+):[^}]+\}', '{$1}'
    $segments = New-Object System.Collections.Generic.List[string]
    $variables = New-Object System.Collections.Generic.List[object]
    foreach ($segment in ($pathNoConstraints.Trim('/') -split '/')) {
        if ($segment -match '^\{(\w+)\}$') {
            $segments.Add(":$($Matches[1])")
            $variables.Add([ordered]@{ key = $Matches[1]; value = "{{$(Get-PathVariableName $pathNoConstraints $Matches[1])}}" })
        }
        else { $segments.Add($segment) }
    }

    $query = New-Object System.Collections.Generic.List[object]
    foreach ($p in @($operation.parameters) | Where-Object { $_ -and $_.in -eq 'query' }) {
        $example = Get-SchemaExample $p.schema $components $p.name 0
        if ($example -is [bool]) { $example = "$example".ToLowerInvariant() }
        $query.Add([ordered]@{ key = $p.name; value = "$example"; disabled = $true; description = "$($p.description)".Trim() })
    }

    $raw = '{{baseUrl}}/' + ($segments -join '/')
    $url = [ordered]@{ raw = $raw; host = @('{{baseUrl}}'); path = $segments }
    if ($variables.Count -gt 0) { $url.variable = $variables }
    if ($query.Count -gt 0) { $url.query = $query }

    $request = [ordered]@{ method = $method.ToUpperInvariant(); header = @(); url = $url }
    $jsonBody = $operation.requestBody.content.'application/json'
    if ($jsonBody) {
        $example = Get-SchemaExample $jsonBody.schema $components $null 0
        $request.header = @([ordered]@{ key = 'Content-Type'; value = 'application/json' })
        $request.body = [ordered]@{ mode = 'raw'; raw = ($example | ConvertTo-Json -Depth 20); options = [ordered]@{ raw = [ordered]@{ language = 'json' } } }
    }
    # Operations without a security requirement are anonymous (see AuthorizeOperationFilter).
    if (-not $operation.security) { $request.auth = [ordered]@{ type = 'noauth' } }
    if ($operation.description -or $operation.summary) { $request.description = "$($operation.summary)`n`n$($operation.description)".Trim() }

    $name = if ($operation.summary) { "$($method.ToUpperInvariant()) $pathNoConstraints - $($operation.summary.Split('.')[0].Trim())" } else { "$($method.ToUpperInvariant()) $pathNoConstraints" }
    $item = [ordered]@{ name = $name; request = $request }
    $events = Get-TestScript $method.ToUpperInvariant() $pathNoConstraints
    if ($events) { $item.event = $events }
    return $item
}

function ConvertTo-PostmanCollection([string]$swaggerPath, [string]$outputPath, [string]$baseUrl) {
    $openApi = Get-Content $swaggerPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $components = $openApi.components.schemas
    $folders = [ordered]@{}
    foreach ($tag in 'Auth', 'Users', 'Projects', 'Tasks', 'Dashboard', 'Ai') { $folders[$tag] = New-Object System.Collections.Generic.List[object] }

    foreach ($pathProp in $openApi.paths.PSObject.Properties) {
        foreach ($opProp in $pathProp.Value.PSObject.Properties) {
            $op = $opProp.Value
            $tag = @($op.tags)[0]; if (-not $tag) { $tag = 'Other' }
            if (-not $folders.Contains($tag)) { $folders[$tag] = New-Object System.Collections.Generic.List[object] }
            $folders[$tag].Add((New-PostmanRequest $opProp.Name $pathProp.Name $op $components))
        }
    }

    # Minimal-API health checks are not part of the OpenAPI document; add the request by hand.
    $health = New-Object System.Collections.Generic.List[object]
    $health.Add([ordered]@{
            name    = 'GET /health - Health check (database)'
            request = [ordered]@{
                method = 'GET'; header = @(); auth = [ordered]@{ type = 'noauth' }
                url = [ordered]@{ raw = '{{baseUrl}}/health'; host = @('{{baseUrl}}'); path = @('health') }
                description = 'Anonymous and not rate limited. 200 when healthy, 503 when a check (database) fails.'
            }
        })
    $folders['Health'] = $health

    $items = New-Object System.Collections.Generic.List[object]
    foreach ($key in $folders.Keys) { if ($folders[$key].Count -gt 0) { $items.Add([ordered]@{ name = $key; item = $folders[$key] }) } }

    $vars = New-Object System.Collections.Generic.List[object]
    foreach ($v in @(@('baseUrl', $baseUrl), @('accessToken', ''), @('userId', ''), @('projectId', ''), @('taskId', ''))) {
        $vars.Add([ordered]@{ key = $v[0]; value = $v[1] })
    }

    $collection = [ordered]@{
        info     = [ordered]@{
            name        = $openApi.info.title
            description = "Generated from docs/swagger.json by tools/export-api-docs.ps1.`n`nSign in with 'POST /api/auth/login' first: it stores the access token in {{accessToken}}, which every request sends as a Bearer token. Create requests store new ids in {{userId}}, {{projectId}} and {{taskId}}. The refresh token is an httpOnly cookie that Postman keeps automatically. Requests marked 'No Auth' are anonymous."
            schema      = 'https://schema.getpostman.com/json/collection/v2.1.0/collection.json'
        }
        auth     = [ordered]@{ type = 'bearer'; bearer = @([ordered]@{ key = 'token'; value = '{{accessToken}}'; type = 'string' }) }
        variable = $vars
        item     = $items
    }
    $json = $collection | ConvertTo-Json -Depth 50
    [IO.File]::WriteAllText($outputPath, $json + "`n", (New-Object System.Text.UTF8Encoding $false))
}

# ---------------------------------------------------------------------------------------------

Push-Location $root
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    Invoke-Native 'Restore local tools' { dotnet tool restore }
    Invoke-Native 'Build API (Release)' { dotnet build src/SmartTaskManager.Api -c Release -nologo -v q }

    Invoke-Native 'Write docs/database.sql (idempotent migrations)' {
        dotnet ef migrations script --idempotent `
            --project src/SmartTaskManager.Infrastructure --startup-project src/SmartTaskManager.Api `
            --configuration Release --no-build --output docs/database.sql
    }

    # The CLI uses the current directory as content root; run it from the build output so the copied
    # appsettings*.json files are found.
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $outputDir = Join-Path $root 'src/SmartTaskManager.Api/bin/Release/net10.0'
    $swaggerFile = Join-Path $docs 'swagger.json'
    Push-Location $outputDir
    try { Invoke-Native 'Write docs/swagger.json' { dotnet swagger tofile --output $swaggerFile SmartTaskManager.Api.dll v1 } }
    finally { Pop-Location }

    Write-Host '==> Write docs/SmartTaskManager.postman_collection.json'
    ConvertTo-PostmanCollection (Join-Path $docs 'swagger.json') (Join-Path $docs 'SmartTaskManager.postman_collection.json') $BaseUrl
    Write-Host 'Done.'
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    Pop-Location
}
