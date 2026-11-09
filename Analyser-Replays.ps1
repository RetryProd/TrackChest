param(
    [string]$Dossier,
    [string]$Donnees
)

$ErrorActionPreference = 'Stop'
$application = Join-Path $PSScriptRoot 'dist\TrackChest.exe'
if (-not (Test-Path -LiteralPath $application -PathType Leaf)) {
    throw 'TrackChest.exe est absent. Lancez Construire.ps1.'
}

$options = @('--once')
if ($Dossier) { $options += @('--demos', $Dossier) }
if ($Donnees) { $options += @('--data', $Donnees) }
& $application @options | Out-Host
exit $LASTEXITCODE
