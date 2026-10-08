param([string]$NsisCompiler)

$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'Source\JoystickMediaControl.csproj'
$appDirectory = Join-Path $PSScriptRoot 'App'
$testDirectory = Join-Path $PSScriptRoot 'TestResults'

& dotnet restore $projectFile --configfile (Join-Path $PSScriptRoot 'Source\NuGet.Config')
if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
& dotnet publish $projectFile --no-restore -c Release --self-contained false -o $appDirectory
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

New-Item -ItemType Directory -Force $testDirectory | Out-Null
$previousTestDirectory = $env:JOYSTICK_MEDIA_TEST_DIR
try {
    $env:JOYSTICK_MEDIA_TEST_DIR = $testDirectory
    $testProcess = Start-Process -FilePath (Join-Path $appDirectory 'JoystickMediaControl.exe') -ArgumentList '--self-test' -WindowStyle Hidden -PassThru -Wait
    if ($testProcess.ExitCode -ne 0) { throw "Tests failed: $($testProcess.ExitCode)" }
    Get-Content (Join-Path $testDirectory 'test-results.txt')
} finally {
    $env:JOYSTICK_MEDIA_TEST_DIR = $previousTestDirectory
}

if ($NsisCompiler) {
    if (-not (Test-Path -LiteralPath $NsisCompiler -PathType Leaf)) { throw 'NSIS compiler not found.' }
    New-Item -ItemType Directory -Force (Join-Path $PSScriptRoot 'dist') | Out-Null
    & $NsisCompiler /V2 (Join-Path $PSScriptRoot 'Source\Installer.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
}
