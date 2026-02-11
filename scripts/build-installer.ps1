$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$msiPath = Join-Path $repoRoot "installer/payload/BloxMC.msi"
if (-not (Test-Path $msiPath)) { throw "Missing MSI: /installer/payload/BloxMC.msi" }

$bootstrapperProject = Join-Path $repoRoot "installer/BootstrapperV5/BootstrapperV5.csproj"
$bundleSource = Join-Path $repoRoot "installer/BundleV5/Bundle.wxs"
$releaseDir = Join-Path $repoRoot "release"
$outputExe = Join-Path $releaseDir "BloxMC-Launcher-Setup.exe"
$toolPath = Join-Path $repoRoot "installer/.tools"
$publishLatestDir = Join-Path $repoRoot "launcher/upload/latest"

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw "dotnet SDK 8+ is required on PATH." }

$wixCmd = Get-Command wix -ErrorAction SilentlyContinue
$localWixExe = Join-Path $toolPath "wix.exe"
if ($wixCmd) {
    $wixExe = $wixCmd.Source
} elseif (Test-Path -LiteralPath $localWixExe) {
    $wixExe = $localWixExe
} else {
    New-Item -ItemType Directory -Path $toolPath -Force | Out-Null
    & dotnet tool install wix --tool-path $toolPath --version "5.*" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to install local WiX v5 tool." }
    $wixExe = $localWixExe
}

$wixVersion = (& $wixExe --version).Trim()
if (-not $wixVersion.StartsWith("5.")) { throw "WiX v5 required. Found: $wixVersion" }

New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null

& dotnet publish $bootstrapperProject -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$wixExt = "WixToolset.BootstrapperApplications.wixext"
$extList = & $wixExe extension list -g 2>$null
if ($LASTEXITCODE -ne 0) { throw "Failed to query WiX extensions." }
if (-not ($extList -match [regex]::Escape($wixExt))) {
    & $wixExe extension add -g "$wixExt/5.0.2" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to install WiX extension $wixExt." }
}

$bundleDir = Split-Path -Parent $bundleSource
Push-Location $bundleDir
try {
    & $wixExe build "Bundle.wxs" -arch x64 -ext $wixExt -out $outputExe
    if ($LASTEXITCODE -ne 0) { throw "wix build failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

Write-Host "Built: $outputExe"
New-Item -ItemType Directory -Path $publishLatestDir -Force | Out-Null
$publishedInstaller = Join-Path $publishLatestDir "BloxMC-Launcher-Setup.exe"
Copy-Item -LiteralPath $outputExe -Destination $publishedInstaller -Force

$latestTxtPath = Join-Path $publishLatestDir "latest.txt"
$latestLines = @(
    "Built: $(Get-Date -Format o)"
    "Source: $outputExe"
    "Published: $publishedInstaller"
)
Set-Content -LiteralPath $latestTxtPath -Value $latestLines -Encoding UTF8

$syncLocalScript = Join-Path $PSScriptRoot "sync-local.ps1"
if (Test-Path -LiteralPath $syncLocalScript) {
    & $syncLocalScript -Root $repoRoot
}

Write-Host "Published installer: $publishedInstaller"
Write-Host "Published marker: $latestTxtPath"
Write-Host "Next: Run $publishedInstaller"
