# Builds the VSIX from scratch and checks that the version inside the package is the one in the manifest.
# The incremental build of the VSSDK can reuse a cached manifest from obj/, so a version change would not be packaged.
param(
	[string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "src\CodeStraight.Vsix\CodeStraight.Vsix.csproj"
$manifest = Join-Path $root "src\CodeStraight.Vsix\source.extension.vsixmanifest"

$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -latest -products * -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) { throw "MSBuild de Visual Studio no encontrado." }

Remove-Item -Recurse -Force (Join-Path $root "src\CodeStraight.Vsix\bin"), (Join-Path $root "src\CodeStraight.Vsix\obj") -ErrorAction SilentlyContinue

dotnet test (Join-Path $root "tests\CodeStraight.Core.Tests") --nologo
if ($LASTEXITCODE -ne 0) { throw "Los tests fallan." }

& $msbuild $project -restore "-p:Configuration=$Configuration" -v:minimal -nologo
if ($LASTEXITCODE -ne 0) { throw "La compilación del VSIX falla." }

$expected = ([xml](Get-Content $manifest)).PackageManifest.Metadata.Identity.Version
$vsix = Join-Path $root "src\CodeStraight.Vsix\bin\$Configuration\net472\CodeStraight.vsix"
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($vsix)
try {
	$entry = $zip.GetEntry("extension.vsixmanifest")
	$reader = New-Object System.IO.StreamReader($entry.Open())
	$packaged = ([xml]$reader.ReadToEnd()).PackageManifest.Metadata.Identity.Version
	$reader.Dispose()
}
finally {
	$zip.Dispose()
}

if ($packaged -ne $expected) { throw "El VSIX contiene la versión $packaged y el manifiesto dice $expected." }
Write-Host "VSIX $packaged listo: $vsix"
