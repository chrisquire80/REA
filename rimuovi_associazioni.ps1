<#
  Annulla installa_associazioni.ps1: rimuove le chiavi di registro di NotepadREA (utente corrente).
  -EliminaProgramma cancella anche %LOCALAPPDATA%\Programs\NotepadREA.
  Le impostazioni/backup in %APPDATA%\NotepadREA non vengono toccati.
#>
param([switch]$EliminaProgramma)
$cu = [Microsoft.Win32.Registry]::CurrentUser
foreach ($p in 'Software\Classes\NotepadREA.Document', 'Software\Classes\Applications\NotepadREA.exe',
               'Software\NotepadREA', 'Software\Classes\*\shell\NotepadREA') {
    try { $cu.DeleteSubKeyTree($p, $false) } catch {}
}
$ra = $cu.OpenSubKey('Software\RegisteredApplications', $true)
if ($ra) { $ra.DeleteValue('NotepadREA', $false); $ra.Close() }
$classes = $cu.OpenSubKey('Software\Classes')
foreach ($e in $classes.GetSubKeyNames() | Where-Object { $_ -like '.*' }) {
    $k = $cu.OpenSubKey("Software\Classes\$e\OpenWithProgids", $true)
    if ($k) { $k.DeleteValue('NotepadREA.Document', $false); $k.Close() }
}
if ($EliminaProgramma) { Remove-Item (Join-Path $env:LOCALAPPDATA 'Programs\NotepadREA') -Recurse -Force -ErrorAction SilentlyContinue }
Add-Type -Namespace Win32 -Name Shell -MemberDefinition '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int e, uint f, IntPtr a, IntPtr b);'
[Win32.Shell]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
"Associazioni NotepadREA rimosse."
