$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# Start-Process -Wait includes descendants: compiler servers must not outlive
# a finished suite and keep the integration task waiting.
$env:UseSharedCompilation = 'false'
$env:DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER = '1'
$root = 'C:\LuckerParty'
$request = Get-Content "$root\request.json" -Raw | ConvertFrom-Json
if ($request.id -notmatch '^run-[0-9]+$' -or $request.suite -notin @('game', 'distribution', 'all')) { throw 'Invalid integration request' }
$output = "$root\runs\$($request.id)"
if (Test-Path $output) { throw 'Run directory already exists; refusing to reuse it' }
New-Item -ItemType Directory $output | Out-Null
$status = @{ state = 'running'; phase = 'preparing'; id = $request.id; suite = $request.suite; started = [DateTime]::UtcNow.ToString('o'); session = (Get-Process -Id $PID).SessionId }
function Save-Status {
    $status | ConvertTo-Json | Set-Content "$output\status.tmp" -Encoding UTF8
    # Replace atomically; transient readers or antivirus must not lose a result.
    for ($attempt = 0; ; $attempt++) {
        try {
            if (Test-Path "$output\status.json") {
                [System.IO.File]::Replace("$output\status.tmp", "$output\status.json", [NullString]::Value)
            } else {
                [System.IO.File]::Move("$output\status.tmp", "$output\status.json")
            }
            return
        } catch {
            if ($attempt -ge 19) { throw }
            Start-Sleep -Milliseconds 50
        }
    }
}
function Invoke-Check([string]$phase, [string[]]$arguments) {
    $status.phase = $phase
    Save-Status
    # Native grandchildren can bypass PowerShell transcription. Capture their
    # inherited pipes to files, then include those files in the transcript.
    $process = Start-Process 'C:\Tools\Python\python.exe' -ArgumentList $arguments -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$output\$phase.stdout.log" -RedirectStandardError "$output\$phase.stderr.log"
    Get-Content "$output\$phase.stdout.log"
    Get-Content "$output\$phase.stderr.log"
    $status["${phase}ExitCode"] = $process.ExitCode
    if ($process.ExitCode -ne 0) { throw "$phase checks failed: $($process.ExitCode)" }
}
Save-Status
Start-Transcript "$output\integration.log"
try {
    if ($status.session -eq 0) { throw 'Integration must run in the logged-in desktop session' }
    $archive = "$root\incoming\$($request.id).zip"
    if ((Get-FileHash $archive).Hash -ne $request.sha256) { throw 'Source archive hash mismatch' }
    $bundle = "$root\incoming\$($request.id).bundle"
    if ((Get-FileHash $bundle).Hash -ne $request.bundleSha256) { throw 'Git bundle hash mismatch' }
    $env:PATH = "C:\Tools\Python;C:\Tools\dotnet;C:\Tools\Git\cmd;$env:PATH"
    & git.exe init -q "$output\source"
    if ($LASTEXITCODE -ne 0) { throw 'Git initialization failed' }
    Set-Location "$output\source"
    & git.exe config core.autocrlf false
    & git.exe fetch -q $bundle HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git bundle import failed' }
    # Populate HEAD and the index without materializing committed files. Overlay
    # exactly the current host tree, preserving edits, untracked files and deletions.
    & git.exe reset --mixed -q FETCH_HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Git snapshot index setup failed' }
    & python.exe -m zipfile -e $archive "$output\source"
    if ($LASTEXITCODE -ne 0) { throw 'Source extraction failed' }
    $env:DOTNET_ROOT = 'C:\Tools\dotnet'
    $env:GODOT_BIN = (Get-ChildItem 'C:\Tools\Godot' -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1).FullName
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:PYTHONUNBUFFERED = '1'
    & python.exe --version
    & dotnet.exe --version
    if ($LASTEXITCODE -ne 0) { throw '.NET SDK startup failed' }
    if ($request.suite -in @('game', 'all')) {
        Invoke-Check 'game' @('tools/dev.py', 'check')
    }
    if ($request.suite -in @('distribution', 'all')) {
        Invoke-Check 'distribution' @('tools/check_distribution.py', '--target', 'windows')
    }
    $status.state = 'passed'
} catch {
    $status.state = 'failed'
    $status.error = $_.ToString()
    Write-Output ($_ | Out-String)
} finally {
    $status.finished = [DateTime]::UtcNow.ToString('o')
    try { Save-Status } finally { Stop-Transcript }
}
