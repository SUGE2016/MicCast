[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Install .NET SDK 10.0.115.'
    }
    $referencePath = Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\mscorlib.dll'
    if (-not (Test-Path -LiteralPath $referencePath)) {
        throw 'Install .NET Framework 4.7.2 Developer Pack.'
    }
    & dotnet msbuild MicCast.csproj /t:Rebuild /p:Configuration=Release /nologo /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $LASTEXITCODE" }
    Get-Item -LiteralPath (Join-Path $PSScriptRoot 'bin\Release\MicCast.exe')
} finally {
    Pop-Location
}
