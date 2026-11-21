# Builds Clawd.exe and the installer. Needs: .NET 8 SDK + Inno Setup 6 or newer (https://jrsoftware.org/isdl.php)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

dotnet publish "$root\src\Clawd\Clawd.csproj" -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "$root\publish"

$iscc = Get-ChildItem -Path @("${env:ProgramFiles(x86)}", $env:ProgramFiles, "$env:LOCALAPPDATA\Programs") `
    -Filter "Inno Setup*" -Directory -ErrorAction SilentlyContinue |
    ForEach-Object { Join-Path $_.FullName "ISCC.exe" } |
    Where-Object { Test-Path $_ } | Select-Object -Last 1
if (-not $iscc) {
    Write-Host "Inno Setup not found. Install it from https://jrsoftware.org/isdl.php and run again." -ForegroundColor Yellow
    exit 1
}
& $iscc "$root\installer\Clawd.iss"
Write-Host "Done: $root\dist\" -ForegroundColor Green
