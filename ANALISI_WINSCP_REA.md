# WinSCP 6.7.1: analisi e ricostruzione (WinSCPREA)

## 1. Input e metodo

| Elemento | Valore |
|---|---|
| Binario analizzato | `C:\Program Files (x86)\WinSCP\WinSCP.exe` |
| Versione | 6.7.1.17902 (prodotto 6.7.1.0) |
| Dimensione | 20.417.088 byte |
| SHA-256 | `8fdc33c186f0052cc496f9e03a9f927eecd505fc9b608731558c64db000fd320` |
| Formato | PE32, x86, sottosistema Windows GUI, 9 sezioni |
| Firma | Authenticode valida; firmatario Martin Přikryl |
| Altri componenti presenti | `WinSCP.com`, `WinSCPnet.dll`, estensioni PowerShell, PuTTY e traduzioni |

La ricognizione locale ha letto i metadati PE, la tabella d’importazione e la firma Authenticode. Le funzionalità del prodotto sono state confrontate con la documentazione pubblica di WinSCP; il codice sorgente e le risorse grafiche dell’applicazione installata non sono stati riutilizzati.

## 2. Dati statici osservati

Il binario importa DLL Win32 per rete e autenticazione (`WS2_32`, `SECUR32`, `CRYPT32`, `WINHTTP`), shell e file system (`SHELL32`, `SHLWAPI`, `MPR`), interfaccia Windows (`USER32`, `GDI32`, `COMCTL32`, `COMDLG32`, `DWMAPI`, `UXTHEME`) e supporto di sistema. La presenza di un import indica una dipendenza tecnica, non da sola una specifica funzionalità visibile.

WinSCP è un client di trasferimento e file manager. La documentazione ufficiale elenca SFTP, SCP, FTP/FTPS, WebDAV e S3, oltre a scripting e sincronizzazione. La copia installata contiene inoltre l’interfaccia da console `WinSCP.com` e la libreria .NET `WinSCPnet.dll`.

## 3. Ricostruzione: WinSCPREA 1.0.0

WinSCPREA è un client desktop scritto da zero in C# / WinForms .NET 10. Per il protocollo SFTP usa SSH.NET 2026.0.1 (licenza MIT), senza appoggiarsi a `WinSCP.exe`, `WinSCP.com` o `WinSCPnet.dll`.

| Area | Implementato |
|---|---|
| Interfaccia | File manager a due pannelli, locale e remoto, con navigazione per percorso |
| Connessione | SFTP con nome utente e password oppure chiave privata SSH |
| Verifica server | Impronta host SHA-256, richiesta esplicita al primo contatto e avviso dedicato se cambia |
| Siti | Salvataggio di host, porta, utente, percorso e percorso chiave; le password non vengono salvate |
| Trasferimenti | Caricamento e scaricamento multiplo, ricorsivo per cartelle, con stato di avanzamento |
| File | Creazione cartelle, rinomina, eliminazione con conferma, apertura dei file locali con l’app predefinita |
| Scorciatoie | F5 aggiorna; Ctrl+U carica; Ctrl+D scarica; Delete elimina gli elementi selezionati |

Le chiavi host accettate sono memorizzate in `%APPDATA%\WinSCPREA\settings.json`. L’utente può collegarsi senza salvare il sito; le credenziali restano in memoria per la durata del processo.

## 4. Limiti della prima versione

È implementato SFTP. Non sono ancora presenti SCP, FTP/FTPS, WebDAV, S3, sincronizzazione/confronto, coda trasferimenti annullabile, ripresa dei trasferimenti, shell scripting, Pageant/ssh-agent, proxy, editor integrato e terminale remoto. Non sono inclusi codice, icone, loghi o immagini di WinSCP.

## 5. Compilazione e pubblicazione

Richiede il .NET 10 SDK e Windows x64.

```powershell
dotnet build WinSCPREA\WinSCPREA.csproj -c Release
dotnet publish WinSCPREA\WinSCPREA.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o dist
```

Il risultato è `dist\WinSCPREA.exe`. La modalità portatile leggera richiede il .NET 10 Desktop Runtime sul PC di destinazione; il pacchetto suite può includere il runtime usando `crea_pacchetto_portatile.ps1 -SelfContained`.

## Fonti

- [Presentazione e funzionalità di WinSCP](https://winscp.net/eng/docs/introduction)
- [Protocolli supportati](https://winscp.net/eng/docs/protocols)
- [Licenza di WinSCP e licenza separata delle icone](https://winscp.net/eng/docs/license)
- [Documentazione ufficiale SSH.NET](https://sshnet.github.io/SSH.NET/examples.html)
