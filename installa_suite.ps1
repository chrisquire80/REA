# Installa la cartella portatile per l'utente corrente, senza privilegi di amministratore.
# Eseguirlo dalla cartella che contiene NoreaLauncher.exe (lo zip scompattato).
$ErrorActionPreference = "Stop"
$source = $PSScriptRoot
$dest = Join-Path $env:LOCALAPPDATA "Programs\NOREA"
if (-not (Test-Path -LiteralPath (Join-Path $source "NoreaLauncher.exe"))) {
    throw "NoreaLauncher.exe non trovato accanto a questo script."
}

New-Item -ItemType Directory -Path $dest -Force | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $dest -Recurse -Force

$startMenu = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut((Join-Path $startMenu "NOREA Launcher.lnk"))
$shortcut.TargetPath = Join-Path $dest "NoreaLauncher.exe"
$shortcut.WorkingDirectory = $dest
$shortcut.Description = "NOREA Suite"
$shortcut.Save()

Write-Host "NOREA installato in $dest"
Write-Host "Collegamento: $startMenu\NOREA Launcher.lnk"
