param(
    [string]$GodotPath = $env:GODOT_EXE
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (!$GodotPath) {
    $command = Get-Command godot4, godot -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { $GodotPath = $command.Source }
}
if (!$GodotPath) {
    $candidates = @(Get-ChildItem 'D:\Godot*_mono_win64\*console.exe' -ErrorAction SilentlyContinue)
    if ($candidates.Count -eq 1) { $GodotPath = $candidates[0].FullName }
}
if (!$GodotPath -or !(Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw 'Godot .NET executable not found. Pass -GodotPath or set GODOT_EXE.'
}

Push-Location $projectRoot
$previousTestMode = $env:CW_RUN_TESTS
$previousReplay = $env:CW_SUPPLY_REPLAY
$logPath = Join-Path ([System.IO.Path]::GetTempPath()) ('project89-tests-' + [guid]::NewGuid() + '.log')
try {
    & dotnet build 'Project 89.csproj'
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; tests were not started.' }

    $env:CW_RUN_TESTS = '1'
    Remove-Item Env:CW_SUPPLY_REPLAY -ErrorAction SilentlyContinue
    # Windows PowerShell treats native stderr as errors; capture the entire run.
    $ErrorActionPreference = 'Continue'
    try {
        & $GodotPath --headless --path $projectRoot *> $logPath
        $testExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = 'Stop'
    }
    Get-Content -LiteralPath $logPath
    # Existing suites do not all contribute to AllTestsRunner's exit code.
    $failures = Select-String -LiteralPath $logPath -Pattern '\[[^\]]*FAIL\]|\bERROR:|\bSCRIPT ERROR:|\bUnhandled exception\b|\b\d+\s+\w+\s+failed\b'
    $finished = Select-String -LiteralPath $logPath -SimpleMatch '========== TEST RUN FINISHED =========='
    if ($testExit -ne 0 -or $failures -or !$finished) {
        throw "Tests failed or did not finish. Godot exit: $testExit. Log: $logPath"
    }
    Write-Host "Build and tests passed. Log: $logPath"
} finally {
    $env:CW_RUN_TESTS = $previousTestMode
    $env:CW_SUPPLY_REPLAY = $previousReplay
    Pop-Location
}
