# With AiSettings:ReasoningEffort empty (the runner sets it to a blank value), the reasoning_effort
# parameter must not be sent to the provider at all. Uses the fake OpenAI server.
$ctx = Get-ApiChecksContext
$tmp = Start-Suite 'ai-no-reasoning-effort'
$fakeLog = Join-Path $tmp 'fake'; New-Item -ItemType Directory -Force $fakeLog | Out-Null
$fake = Start-Job -FilePath $ctx.FakeAiServer -ArgumentList 7399, $fakeLog
Start-Sleep -Seconds 2
try {
    $admin = Get-SeedAdmin
    $token = Login $admin.Email $admin.Password
    $r = Req POST /api/ai/improve-description @{ description = 'fix login bug' } $token
    $sent = ((Get-ChildItem $fakeLog -Filter *.json | Sort-Object Name | Select-Object -Last 1 | Get-Content -Raw) | ConvertFrom-Json).body | ConvertFrom-Json
    $keys = $sent.PSObject.Properties.Name
    Check 'request succeeds through the fake provider' ($r.Code -eq 200 -and $r.Body.data.source -eq 'ai')
    Check 'empty ReasoningEffort -> reasoning_effort not sent' ($keys -notcontains 'reasoning_effort') "keys=$($keys -join ',')"
}
finally {
    Stop-Job $fake -ErrorAction SilentlyContinue; Remove-Job $fake -Force -ErrorAction SilentlyContinue
}
Complete-Suite
