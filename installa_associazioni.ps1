<#
  Installa NotepadREA per l'utente corrente (nessun diritto di amministratore):
   - copia l'exe in %LOCALAPPDATA%\Programs\NotepadREA
   - lo registra in "Apri con" e in Impostazioni > App predefinite per le estensioni di testo
   - aggiunge "Modifica con NotepadREA" al menu del tasto destro di qualsiasi file
  Windows non consente a un programma di impostarsi come predefinito: l'ultimo clic ("Sempre") resta all'utente.
  Per annullare: rimuovi_associazioni.ps1
#>
param([string]$Sorgente = (Join-Path $PSScriptRoot 'dist\NotepadREA.exe'))
$ErrorActionPreference = 'Stop'

$dest = Join-Path $env:LOCALAPPDATA 'Programs\NotepadREA'
$exe  = Join-Path $dest 'NotepadREA.exe'
$icoFile = Join-Path $dest 'NotepadREA.ico'
$srcIco  = Join-Path $PSScriptRoot 'NotepadREA\app.ico'
if (Get-Process NotepadREA -ErrorAction SilentlyContinue | Where-Object Path -eq $exe) { throw "Chiudi NotepadREA prima di aggiornarlo." }
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item $Sorgente $exe -Force
if (Test-Path $srcIco) { Copy-Item $srcIco $icoFile -Force }

$progId = 'NotepadREA.Document'
$open   = "`"$exe`" `"%1`""
$icon   = if (Test-Path $icoFile) { "`"$icoFile`",0" } else { "`"$exe`",0" }
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
