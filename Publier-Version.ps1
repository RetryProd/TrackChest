<#
.SYNOPSIS
  Fabrique une version de TrackChest (.msi + .zip + annonce signée) et, avec -Publier, la met sur GitHub Releases.
.EXAMPLE
  .\Publier-Version.ps1 -Version 1.1.0 -Notes "Mises à jour automatiques"            # fabrique dans dist\release
  .\Publier-Version.ps1 -Version 1.1.0 -Notes "…" -Publier                          # et crée la release GitHub
.NOTES
  Il faut : le SDK .NET 10, WiX 5 (dotnet tool install wix --version 5.0.2 --tool-path "$env:LOCALAPPDATA\TrackChest\wix"),
  la clé privée de signature ($env:USERPROFILE\TrackChest-cles\addon-private.key) et, pour -Publier, le programme gh connecté.
  Les mises à jour des joueurs sont signées avec la même clé que le catalogue d'add-ons.
#>
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [string]$Notes = '',
    [string]$Cle = (Join-Path $env:USERPROFILE 'TrackChest-cles\addon-private.key'),
    [switch]$Publier
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
$release = Join-Path $dist 'release'
$wix = Join-Path $env:LOCALAPPDATA 'TrackChest\wix\wix.exe'
if (-not (Test-Path -LiteralPath $Cle)) { throw "Clé privée introuvable : $Cle" }
if (-not (Test-Path -LiteralPath $wix)) { throw "WiX introuvable ($wix). Voir l'aide du script." }
if ($Notes.Length -gt 600) { throw 'Les notes sont limitées à 600 caractères.' }

# 1. Numéro de version dans le projet
$csproj = Join-Path $root 'src\ChestCounter\TrackChest.csproj'
$text = [IO.File]::ReadAllText($csproj)
[IO.File]::WriteAllText($csproj, [regex]::Replace($text, '<Version>[^<]*</Version>', "<Version>$Version</Version>"), (New-Object Text.UTF8Encoding($false)))

# 2. Compilation
New-Item -ItemType Directory -Path $release -Force | Out-Null
Get-ChildItem -LiteralPath $release | Remove-Item -Recurse -Force
$dist = Join-Path $release 'build'
& (Join-Path $root 'Construire.ps1') -Sortie $dist
if ($LASTEXITCODE -ne 0) { throw 'La compilation a échoué.' }

# 3. .msi
$msi = Join-Path $release "TrackChest-$Version.msi"
& $wix build (Join-Path $root 'installer\TrackChest.wxs') -d "Version=$Version" -d "Source=$dist" -d "Assets=$(Join-Path $root 'assets')" -arch x64 -ext WixToolset.UI.wixext -culture fr-FR -o $msi
if ($LASTEXITCODE -ne 0) { throw 'La fabrication du .msi a échoué.' }

# 4. .zip portable avec le fonctionnement (LISEZMOI.txt)
$staging = Join-Path $release 'TrackChest'
New-Item -ItemType Directory -Path $staging | Out-Null
foreach ($f in 'TrackChest.exe', 'LICENSE-ChestCounter.txt', 'LICENSE-FortniteReplayDecompressor.txt', 'LICENSE-GPL-3.0.txt', 'CREDITS.md') {
    Copy-Item -LiteralPath (Join-Path $dist $f) -Destination $staging
}
Copy-Item -LiteralPath (Join-Path $root 'installer\LISEZMOI.txt') -Destination $staging
$zip = Join-Path $release "TrackChest-$Version-portable.zip"
Compress-Archive -Path $staging -DestinationPath $zip
Remove-Item -LiteralPath $staging -Recurse -Force

# 5. Annonce de mise à jour, signée
$sha = (Get-FileHash -LiteralPath $msi -Algorithm SHA256).Hash
$info = [ordered]@{ version = $Version; sha256 = $sha; size = (Get-Item -LiteralPath $msi).Length; notes = $Notes }
$json = Join-Path $release 'update.json'
[IO.File]::WriteAllText($json, ($info | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
$exe = Join-Path $dist 'TrackChest.exe'
& $exe --addon-sign $json $Cle | Out-Null
& $exe --addon-verify $json (Join-Path $root 'assets\addon-public.key') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'La signature ne correspond pas à assets\addon-public.key : mauvaise clé privée.' }
Move-Item -LiteralPath "$json.sig" -Destination (Join-Path $release 'update.json.sig') -Force

Write-Output "Version $Version prête dans $release :"
Remove-Item -LiteralPath $dist -Recurse -Force
Get-ChildItem -LiteralPath $release | ForEach-Object { Write-Output ("  {0}  ({1:N1} Mo)" -f $_.Name, ($_.Length / 1MB)) }

if ($Publier) {
    $tag = "v$Version"
    $body = if ($Notes) { $Notes } else { "TrackChest $Version" }
    gh release create $tag --repo RetryProd/TrackChest --title "TrackChest $Version" --notes $body `
        $msi $zip $json (Join-Path $release 'update.json.sig')
    if ($LASTEXITCODE -ne 0) { throw 'La création de la release GitHub a échoué.' }
    Write-Output "Release publiée : https://github.com/RetryProd/TrackChest/releases/tag/$tag"
}
