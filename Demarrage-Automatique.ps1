param([switch]$Desactiver)

$ErrorActionPreference = 'Stop'
$startup = [Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
$shortcutPath = Join-Path $startup 'TrackChest avec Fortnite.lnk'
# Raccourci de l'ancien nom (ChestOpen).
$oldShortcut = Join-Path $startup 'ChestOpen avec Fortnite.lnk'
if (Test-Path -LiteralPath $oldShortcut) { Remove-Item -LiteralPath $oldShortcut }

if ($Desactiver) {
    if (Test-Path -LiteralPath $shortcutPath) { Remove-Item -LiteralPath $shortcutPath }
    Write-Output 'Demarrage automatique desactive.'
    return
}

$application = Join-Path $PSScriptRoot 'dist\TrackChest.exe'
if (-not (Test-Path -LiteralPath $application -PathType Leaf)) {
    throw 'TrackChest.exe est absent. Lancez Construire.ps1.'
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $application
$shortcut.Arguments = '--watch-fortnite'
$shortcut.WorkingDirectory = Split-Path -Parent $application
$shortcut.Description = 'Ouvre TrackChest au lancement de Fortnite'
$shortcut.Save()

Start-Process -FilePath $application -ArgumentList '--watch-fortnite' -WindowStyle Hidden
Write-Output 'Demarrage automatique active pour ce compte Windows.'
