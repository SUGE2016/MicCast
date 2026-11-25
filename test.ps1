[CmdletBinding()]
param([switch]$Hardware)
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & "$PSScriptRoot\build.ps1"
    & dotnet msbuild tests\LogicTests.csproj /t:Rebuild /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'Test build failed.' }
    & "$PSScriptRoot\bin\Tests\MicCast.LogicTests.exe"
    if ($LASTEXITCODE -ne 0) { throw 'Logic tests failed.' }
    & dotnet msbuild tests\AppSmoke.csproj /t:Rebuild /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw 'App smoke test build failed.' }
    $isolated = Join-Path ([IO.Path]::GetTempPath()) ('MicCastSmoke-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $isolated | Out-Null
    $standaloneExe = Join-Path $isolated 'MicCast.exe'
    try {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\MicCast.exe') -Destination $standaloneExe
        & "$PSScriptRoot\bin\Tests\MicCast.AppSmoke.exe" $standaloneExe
        if ($LASTEXITCODE -ne 0) { throw 'Standalone EXE smoke test failed.' }
    } finally {
        Remove-Item -LiteralPath $standaloneExe -ErrorAction SilentlyContinue
        [IO.Directory]::Delete($isolated)
    }
    if ($Hardware) {
        $results = Join-Path $PSScriptRoot 'TestResults'
        New-Item -ItemType Directory -Force -Path $results | Out-Null
        $stdout = Join-Path $results 'hardware.stdout.txt'
        $stderr = Join-Path $results 'hardware.stderr.txt'
        $exe = Join-Path $PSScriptRoot 'bin\Release\MicCast.exe'
        $process = Start-Process -FilePath $exe -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        Get-Content -LiteralPath $stdout
        if ($process.ExitCode -ne 0) {
            Get-Content -LiteralPath $stderr
            throw "Hardware test failed: $($process.ExitCode). Close MicCast before testing."
        }
        if (-not (Select-String -LiteralPath $stdout -SimpleMatch 'SelfTest=PASS' -Quiet)) { throw 'Hardware test did not produce a success result.' }
    }
} finally { Pop-Location }
