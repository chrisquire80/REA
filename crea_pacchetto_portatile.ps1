# Script per generare il pacchetto portatile NOREA Suite per un altro PC
param(
    [switch]$SelfContained = $false
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$outDir = Join-Path $root "norea_portable"
$zipPath = Join-Path $root "NOREA_Suite_Portable.zip"

Write-Host "=== Generazione Pacchetto Portatile NOREA Suite ===" -ForegroundColor Cyan

if (Test-Path $outDir) { Remove-Item -Recurse -Force $outDir }
New-Item -ItemType Directory -Path $outDir | Out-Null

if ($SelfContained) {
    Write-Host "[1/3] Compilazione Self-Contained (Zero Dipendenze)..." -ForegroundColor Yellow
    dotnet publish "$root\NotepadREA\NotepadREA.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o "$outDir"
    dotnet publish "$root\NoreaLauncher\NoreaLauncher.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o "$outDir"
    dotnet publish "$root\NoreaCli\NoreaCli.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o "$outDir"
    dotnet publish "$root\WinSCPREA\WinSCPREA.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o "$outDir"
    dotnet publish "$root\EverythingREA\EverythingREA.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o "$outDir"
} else {
    Write-Host "[1/3] Compilazione Portatile ad alte prestazioni..." -ForegroundColor Yellow
    dotnet publish "$root\NotepadREA\NotepadREA.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "$outDir"
    dotnet publish "$root\NoreaLauncher\NoreaLauncher.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "$outDir"
    dotnet publish "$root\NoreaCli\NoreaCli.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "$outDir"
    dotnet publish "$root\WinSCPREA\WinSCPREA.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "$outDir"
    dotnet publish "$root\EverythingREA\EverythingREA.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o "$outDir"
}
if (Test-Path "$outDir\NoreaCli.exe") { Copy-Item "$outDir\NoreaCli.exe" "$outDir\norea.exe" -Force }
Set-Content -Path "$outDir\norea.cmd" -Value "@echo off`r`n`"%~dp0norea.exe`" %*" -Encoding ASCII

Write-Host "[2/3] Copia script di utilità e documentazione..." -ForegroundColor Yellow
Copy-Item "$root\installa_associazioni.ps1" -Destination "$outDir\" -Force -ErrorAction SilentlyContinue
Copy-Item "$root\rimuovi_associazioni.ps1" -Destination "$outDir\" -Force -ErrorAction SilentlyContinue
Copy-Item "$root\THIRD_PARTY_NOTICES.md" -Destination "$outDir\" -Force
Copy-Item "$root\ANALISI_WINSCP_REA.md" -Destination "$outDir\" -Force
Copy-Item "$root\ANALISI_EVERYTHING_REA.md" -Destination "$outDir\" -Force

$readmeContent = @"
==================================================
           NOREA Suite — Versione Portatile
==================================================

COME USARE LA SUITE SU QUESTO PC:

1. Avvio immediato:
   - Fai doppio clic su "NoreaLauncher.exe" per aprire il launcher della suite.
   - Oppure apri direttamente "NotepadREA.exe" per l'editor.
   - Apri "WinSCPREA.exe" per il client SFTP a due pannelli.

2. Requisiti:
   - Windows 10 o Windows 11 (x64).
   - Se usi la versione leggera, e non hai .NET 10 Desktop Runtime,
     Windows ti proporra' automaticamente di scaricarlo in 30 secondi.

3. Associazioni file opzionali:
   - Se desideri aprire automaticamente file .txt, .cs, .py, .json, .log con NotepadREA,
     fai clic destro su "installa_associazioni.ps1" ed esegui con PowerShell.

Non richiede installazione ne' privilegi di amministratore per funzionare!
"@
Set-Content -Path (Join-Path $outDir "LEGGIMI_PORTATILE.txt") -Value $readmeContent -Encoding UTF8

Write-Host "[3/3] Compressione in archivio ZIP..." -ForegroundColor Yellow
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -Force

$zipItem = Get-Item $zipPath
$sizeMb = [Math]::Round($zipItem.Length / 1MB, 2)
Write-Host "Pacchetto creato con successo!" -ForegroundColor Green
Write-Host "Percorso ZIP: $zipPath ($sizeMb MB)" -ForegroundColor Cyan
Write-Host "Cartella:     $outDir" -ForegroundColor Cyan
