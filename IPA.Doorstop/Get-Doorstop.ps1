#Requires -Version 7.0
param(
    [ValidateSet('release', 'debug')]
    [string] $Variant = 'release'
)

$ErrorActionPreference = 'Stop'

$targetAssembly = 'IPA\IPA.Doorstop.dll'
$version = ([xml](Get-Content -Raw "$PSScriptRoot\IPA.Doorstop.csproj")).SelectSingleNode('//DoorstopVersion').InnerText

$dir = "$PSScriptRoot\obj\doorstop\$version-$Variant"
$zipName = "doorstop_win_${Variant}_$version.zip"
$zip = "$dir\$zipName"

if (-not (Test-Path $zip)) {
    New-Item -ItemType Directory -Force $dir | Out-Null
    Invoke-WebRequest "https://github.com/NeighTools/UnityDoorstop/releases/download/v$version/$zipName" -OutFile "$zip.part"
    Move-Item -Force "$zip.part" $zip
}

Expand-Archive $zip $dir -Force

$config = Get-Content -Raw "$dir\x64\doorstop_config.ini"
Set-Content "$dir\doorstop_config.ini" ($config -replace '(?m)^target_assembly\s*=[^\r\n]*', "target_assembly=$targetAssembly") -NoNewline
