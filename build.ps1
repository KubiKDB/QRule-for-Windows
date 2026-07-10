<#
    build.ps1 — Windows build for QRule W.

    Runs on Windows only. Publishes the app, then assembles and packs an MSIX with makeappx,
    and (optionally) signs it for local install. Store submission uses the unsigned .msix.

    Prereqs: .NET 8 SDK, Windows 10/11 SDK (makeappx.exe / signtool.exe on PATH or under
    "C:\Program Files (x86)\Windows Kits\10\bin\<ver>\x64").

    Usage:
        .\build.ps1                       # publish + pack MSIX (unsigned)
        .\build.ps1 -Rid win-arm64        # target ARM64
        .\build.ps1 -Sign -CertThumbprint <thumb>   # sign for sideload install
#>
[CmdletBinding()]
param(
    [string]$Rid = "win-x64",
    [string]$Configuration = "Release",
    [switch]$Sign,
    [string]$CertThumbprint
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$publishDir = Join-Path "dist" "$Rid\app"
$layoutDir  = Join-Path "dist" "$Rid\msix-layout"
$msixPath   = Join-Path "dist" "$Rid\QRuleW.msix"

Write-Host "==> Publishing $Rid ($Configuration)" -ForegroundColor Cyan
dotnet publish src/QRuleW/QRuleW.csproj `
    -c $Configuration -r $Rid --self-contained true `
    -p:PublishSingleFile=false `
    -o $publishDir

Write-Host "==> Assembling MSIX layout" -ForegroundColor Cyan
if (Test-Path $layoutDir) { Remove-Item $layoutDir -Recurse -Force }
New-Item -ItemType Directory -Path $layoutDir | Out-Null
Copy-Item "$publishDir\*" $layoutDir -Recurse
Copy-Item "src/QRuleW.Package/Package.appxmanifest" (Join-Path $layoutDir "AppxManifest.xml")
Copy-Item "src/QRuleW.Package/Images" (Join-Path $layoutDir "Images") -Recurse

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $root = "C:\Program Files (x86)\Windows Kits\10\bin"
    $tool = Get-ChildItem $root -Recurse -Filter $name -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match "x64" } |
            Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $tool) { throw "$name not found. Install the Windows 10/11 SDK." }
    return $tool.FullName
}

$makeappx = Find-SdkTool "makeappx.exe"
Write-Host "==> Packing MSIX with $makeappx" -ForegroundColor Cyan
& $makeappx pack /d $layoutDir /p $msixPath /overwrite

if ($Sign) {
    if (-not $CertThumbprint) { throw "-Sign requires -CertThumbprint." }
    $signtool = Find-SdkTool "signtool.exe"
    Write-Host "==> Signing $msixPath" -ForegroundColor Cyan
    & $signtool sign /fd SHA256 /sha1 $CertThumbprint $msixPath
}

Write-Host ""
Write-Host "==> Done. MSIX: $msixPath" -ForegroundColor Green
Write-Host "    Unsigned package is for Store submission; use -Sign to sideload-install locally."
