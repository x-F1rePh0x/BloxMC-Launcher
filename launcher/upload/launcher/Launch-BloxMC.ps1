$ErrorActionPreference = "Stop"
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path

$jar = Get-ChildItem -Path $dir -Filter "launcher-*.jar" |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $jar) {
    Write-Host "No launcher-*.jar found in $dir"
    Read-Host "Press Enter to exit"
    exit 1
}

$javawCandidates = @(
    "C:\Program Files\Common Files\Oracle\Java\javapath\javaw.exe",
    "C:\Program Files\Java\jdk-21\bin\javaw.exe"
)

$javaw = $javawCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $javaw) {
    $cmd = Get-Command javaw -ErrorAction SilentlyContinue
    if ($cmd) { $javaw = $cmd.Source }
}

if (-not $javaw) {
    Write-Host "Java not found. Install Java 21+."
    Read-Host "Press Enter to exit"
    exit 1
}

$arguments = @("-jar", $jar.FullName)
Start-Process -FilePath $javaw -ArgumentList $arguments -WorkingDirectory $dir
