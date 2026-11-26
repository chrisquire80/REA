<#
  Installa NotepadREA per l'utente corrente (nessun diritto di amministratore):
   - copia l'exe in %LOCALAPPDATA%\Programs\NotepadREA
   - lo registra in "Apri con" e in Impostazioni > App predefinite per le estensioni di testo
   - aggiunge "Modifica con NotepadREA" al menu del tasto destro di qualsiasi file
  Windows non consente a un programma di impostarsi come predefinito: l'ultimo clic ("Sempre") resta all'utente.
  Per annullare: rimuovi_associazioni.ps1

  Senza -Sorgente lo script accetta entrambi i layout:
   - albero del repository: dist\NotepadREA.exe accanto a questo script
   - pacchetto portatile: NotepadREA.exe nella stessa cartella dello script
#>
param([string]$Sorgente)
$ErrorActionPreference = 'Stop'

function Resolve-NotepadReaSource([string]$Explicit) {
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (-not (Test-Path -LiteralPath $Explicit -PathType Leaf)) {
            throw "Eseguibile non trovato: $Explicit"
        }
        return (Resolve-Path -LiteralPath $Explicit).Path
    }

    $candidates = @(
        (Join-Path $PSScriptRoot 'dist\NotepadREA.exe'),
        (Join-Path $PSScriptRoot 'NotepadREA.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    throw "NotepadREA.exe non trovato. Cercato in:`n - $($candidates -join "`n - ")"
}

$Sorgente = Resolve-NotepadReaSource $Sorgente
$dest = Join-Path $env:LOCALAPPDATA 'Programs\NotepadREA'
$exe  = Join-Path $dest 'NotepadREA.exe'
$icoFile = Join-Path $dest 'NotepadREA.ico'
$icoCandidates = @(
    (Join-Path $PSScriptRoot 'NotepadREA\app.ico'),
    (Join-Path (Split-Path -Parent $Sorgente) 'NotepadREA.ico'),
    (Join-Path $PSScriptRoot 'app.ico')
)
$srcIco = $icoCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
if (Get-Process NotepadREA -ErrorAction SilentlyContinue | Where-Object Path -eq $exe) { throw "Chiudi NotepadREA prima di aggiornarlo." }
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item -LiteralPath $Sorgente $exe -Force
if ($srcIco) { Copy-Item -LiteralPath $srcIco $icoFile -Force }

$progId = 'NotepadREA.Document'
$open   = "`"$exe`" `"%1`""
$icon   = if (Test-Path $icoFile) { "`"$icoFile`"" } else { "`"$exe`",0" }
$ext = '.txt','.log','.ini','.cfg','.conf','.config','.json','.xml','.md','.markdown','.csv','.yml','.yaml','.toml',
       '.properties','.nfo','.ps1','.psm1','.psd1','.bat','.cmd','.py','.js','.mjs','.ts','.css','.scss','.html','.htm',
       '.sql','.cs','.c','.cpp','.h','.hpp','.java','.sh','.editorconfig','.gitignore','.env'

$cu = [Microsoft.Win32.Registry]::CurrentUser
function Set-Key([string]$path, [hashtable]$values) {
    $k = $cu.CreateSubKey($path)
    foreach ($n in $values.Keys) { $k.SetValue($(if ($n -eq '(default)') { '' } else { $n }), $values[$n]) }
    $k.Close()
}

# ProgID del documento
Set-Key "Software\Classes\$progId" @{ '(default)' = 'Documento di testo (NotepadREA)'; 'FriendlyTypeName' = 'Documento di testo (NotepadREA)' }
Set-Key "Software\Classes\$progId\DefaultIcon" @{ '(default)' = $icon }
Set-Key "Software\Classes\$progId\shell\open\command" @{ '(default)' = $open }

# Applicazione (voce in "Apri con")
Set-Key "Software\Classes\Applications\NotepadREA.exe" @{ 'FriendlyAppName' = 'NotepadREA' }
Set-Key "Software\Classes\Applications\NotepadREA.exe\DefaultIcon" @{ '(default)' = $icon }
Set-Key "Software\Classes\Applications\NotepadREA.exe\shell\open\command" @{ '(default)' = $open }
$st = $cu.CreateSubKey("Software\Classes\Applications\NotepadREA.exe\SupportedTypes")
foreach ($e in $ext) { $st.SetValue($e, '') }; $st.Close()

# Capabilities (Impostazioni > App predefinite)
Set-Key "Software\NotepadREA\Capabilities" @{ 'ApplicationName' = 'NotepadREA'; 'ApplicationDescription' = 'Editor di testo e codice'; 'ApplicationIcon' = $icon }
$fa = $cu.CreateSubKey("Software\NotepadREA\Capabilities\FileAssociations")
foreach ($e in $ext) { $fa.SetValue($e, $progId) }; $fa.Close()
Set-Key "Software\RegisteredApplications" @{ 'NotepadREA' = 'Software\NotepadREA\Capabilities' }

# Proposto per ogni estensione in "Apri con" (non cambia il predefinito attuale)
foreach ($e in $ext) {
    $k = $cu.CreateSubKey("Software\Classes\$e\OpenWithProgids")
    $k.SetValue($progId, [byte[]]@(), [Microsoft.Win32.RegistryValueKind]::None); $k.Close()
}

# Tasto destro su qualsiasi file: "Modifica con NotepadREA"
Set-Key "Software\Classes\*\shell\NotepadREA" @{ '(default)' = 'Modifica con NotepadREA'; 'Icon' = $icon }
Set-Key "Software\Classes\*\shell\NotepadREA\command" @{ '(default)' = $open }

# Avvisa Esplora risorse del cambiamento e aggiorna la cache delle icone
Start-Process ie4uinit.exe -ArgumentList '-show' -NoNewWindow -Wait -ErrorAction SilentlyContinue
Add-Type -Namespace Win32 -Name Shell -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);'
[Win32.Shell]::SHChangeNotify(0x08000000, 0x1000, [IntPtr]::Zero, [IntPtr]::Zero)

"Installato: $exe"
"Icona: $icon"
"Registrate $($ext.Count) estensioni."
