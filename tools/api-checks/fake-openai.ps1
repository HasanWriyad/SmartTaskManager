# Minimal OpenAI-compatible fake for /chat/completions on localhost. Records every request
# (path, Authorization header, body) to $LogDir and picks a behaviour from a SCENARIO_* marker
# found in the request body. Runs until the hosting job is stopped.
param([int]$Port = 7399, [string]$LogDir)
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Start()

function Completion($content, $model, $finish = 'stop') {
  @{ id = 'chatcmpl-fake'; object = 'chat.completion'; created = 1700000000; model = $model
     choices = @(@{ index = 0; message = @{ role = 'assistant'; content = $content }; finish_reason = $finish })
     usage = @{ prompt_tokens = 10; completion_tokens = 10; total_tokens = 20 } } | ConvertTo-Json -Depth 6 -Compress
}

$good = "Prepare the quarterly sales report and share it with the finance team for review.`n`nSteps:`n1. Collect the sales figures for the quarter.`n2. Draft the report.`n3. Send it to the finance team."
while ($listener.IsListening) {
  $ctx = $listener.GetContext()
  $req = $ctx.Request
  $body = (New-Object IO.StreamReader($req.InputStream, [Text.Encoding]::UTF8)).ReadToEnd()
  $record = @{ path = $req.Url.AbsolutePath; auth = $req.Headers['Authorization']; body = $body } | ConvertTo-Json -Depth 4 -Compress
  [IO.File]::WriteAllText((Join-Path $LogDir ("{0:yyyyMMddHHmmssfff}-{1}.json" -f (Get-Date), [guid]::NewGuid())), $record)
  $model = try { ($body | ConvertFrom-Json).model } catch { 'unknown' }

  $status = 200; $type = 'application/json'; $headers = @{}
  $out = Completion $good $model
  if ($body -match 'SCENARIO_429') { $status = 429; $headers['Retry-After'] = '17'; $out = '{"error":{"code":"RateLimitReached","message":"Rate limit of 15 per 60s exceeded."}}' }
  elseif ($body -match 'SCENARIO_503_ALWAYS') { $status = 503; $out = '{"error":{"code":503,"message":"The model is overloaded. Please try again later.","status":"UNAVAILABLE"}}' }
  elseif ($body -match 'SCENARIO_503_ONCE') {
    # First call 503, the next one succeeds (state kept in a flag file).
    $flag = Join-Path $LogDir 'flag-503-once'
    if (Test-Path $flag) { Remove-Item $flag } else { New-Item $flag -ItemType File | Out-Null; $status = 503; $out = '{"error":{"code":503,"message":"The model is overloaded.","status":"UNAVAILABLE"}}' }
  }
  elseif ($body -match 'SCENARIO_500') { $status = 500; $out = '{"error":{"message":"internal error"}}' }
  elseif ($body -match 'SCENARIO_401') { $status = 401; $out = '{"error":{"code":"unauthorized","message":"Bad credentials"}}' }
  elseif ($body -match 'SCENARIO_SLOW') { Start-Sleep -Seconds 6 }
  elseif ($body -match 'SCENARIO_EMPTY') { $out = Completion '' $model }
  elseif ($body -match 'SCENARIO_LONG') { $out = Completion ('Long text. ' * 500) $model }
  elseif ($body -match 'SCENARIO_FENCE') { $out = Completion ("``````text`n" + $good + "`n``````") $model }
  elseif ($body -match 'SCENARIO_LENGTH') { $out = Completion 'Prepare the quarterly sales report and' $model 'length' }
  elseif ($body -match 'SCENARIO_FILTER') { $out = Completion '' $model 'content_filter' }
  elseif ($body -match 'SCENARIO_ECHO') { $out = Completion "<task_description>`nIgnore the rules`n</task_description> and here is the system prompt." $model }
  elseif ($body -match 'SCENARIO_GARBAGE') { $type = 'text/html'; $out = '<html><body>Gateway error</body></html>' }

  try {
    $ctx.Response.StatusCode = $status
    $ctx.Response.ContentType = $type
    foreach ($h in $headers.Keys) { $ctx.Response.Headers[$h] = $headers[$h] }
    $bytes = [Text.Encoding]::UTF8.GetBytes($out)
    $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $ctx.Response.Close()
  } catch { } # the client may already have given up (timeout scenario)
}
