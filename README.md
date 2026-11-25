# NOREA — Reverse Engineering & Clean-Room Reconstruction Suite

[![Build & Release](https://github.com/chrisquire80/REA/actions/workflows/release.yml/badge.svg)](https://github.com/chrisquire80/REA/actions/workflows/release.yml)
![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/Platform-Windows%20x64-0078D6?logo=windows&logoColor=white)
![Tests](https://img.shields.io/badge/xUnit-51%20Passed-brightgreen?logo=xunit)
[![Release](https://img.shields.io/github/v/release/chrisquire80/REA?color=blue&label=Latest%20Release)](https://github.com/chrisquire80/REA/releases)
![License](https://img.shields.io/badge/License-MIT-orange)

**NOREA** è una suite di ingegneria inversa *clean-room* e ricostruzione nativa ad altissime prestazioni in **C# / .NET 10 Windows**, progettata per analizzare software Win32 legacy ed estrarre componenti, architettura e funzionalità in implementazioni moderne, sicure e modulari con tempo di avvio istantaneo (**< 400 ms**).

---

## 📸 Galleria e Interfaccia

| NoreaLauncher (Master-Detail & Telemetria Live) | Impostazioni & Integrazione PATH / Cloud |
| :---: | :---: |
| ![NoreaLauncher Suite](docs/images/launcher_master_detail.png) | ![NoreaLauncher Settings](docs/images/launcher_settings.png) |

---

## 📦 Progetti della Suite

### 1. [NotepadREA](NotepadREA/NotepadREA.csproj) (v2.1.0)
Editor di testo e codice sorgente completo, ricostruito da zero a partire dall'analisi statica e dinamica di Notepad++ 8.9.8.1 (PE32+ AMD64).
* **Terminale PowerShell/CMD Incorporato (`F12` / `Ctrl+~`)**: Console interattiva integrata in un cassetto a scomparsa, aperta automaticamente nella cartella del documento corrente per compilare o eseguire script.
* **Confronto File Side-by-Side (`Ctrl+Alt+D`)**: Interfaccia a doppio editor Scintilla con scorrimento verticale sincronizzato, evidenziazione a riga intera (aggiunte, rimosse, modificate) e navigazione con `F7` e `Shift+F7`.
* **Indicatori Git Diff nel Margine**: Barre cromatiche a sinistra dei numeri di riga (verde per aggiunte, blu per modifiche, rosso per cancellazioni) aggiornate in background tramite `git diff`.
* **Ispezione Esadecimale (Hex View Modal - `Ctrl+Alt+H`)**: Visualizzatore esadecimale virtualizzato con colonna byte, decodifica ASCII, ricerca e calcolo automatico dell'**Entropia di Shannon** (utile per rilevare file compressi o offuscati).
* **Suite Strumenti & Formattatori**: Formattazione e minificazione JSON (`Ctrl+Shift+J`), formattazione XML, codifica/decodifica Base64 (`Ctrl+Shift+B`) e URL, ordinamento righe alfabetico (`A → Z` / `Z → A`), rimozione duplicati e pulizia spazi di fine riga.
* **Multi-Caret & Selezione a Colonna**: Supporto nativo a cursori multipli (`Ctrl+Clic`), selezione a rettangolo (`Alt+Trascina` o `Shift+Alt+Frecce`).
* **TextCodec puro & Salvataggio Atomico**: Rilevamento automatico codifiche (UTF-8 con/senza BOM, UTF-16 LE/BE, ANSI di sistema), fallback su file temporaneo con `File.Replace`, protezione da sovrascrittura binari accidentale e UAC fallback per file di sistema (`--elevated-save`).
* **Find & Replace in Files**: Finestra multi-thread parallelizzata (`Ctrl+Shift+F`) con supporto a sottocartelle, regex e navigazione rapida.
* **Librerie Native Incorporate (Self-Healing)**: `Scintilla.dll` e `Lexilla.dll` sono incorporate direttamente nell'eseguibile ed auto-estratte al primo avvio se mancanti su disco, rendendo l'eseguibile autonomo al 100%.

### 2. [NoreaLauncher](NoreaLauncher/NoreaLauncher.csproj) (v2.2.2)
Launcher della suite in stile **Master-Detail**, con palette NOREA chiaro/scuro e componenti condivisi.
* **Layout Master-Detail**: Ricerca rapida, elenco applicazioni, stato e azioni principali coerenti con il sistema di design NOREA.
* **Sistema di design riutilizzabile**: Token colore, tipografia, spaziatura e controlli per i prossimi programmi NOREA in [docs/NOREA_DESIGN_SYSTEM.md](docs/NOREA_DESIGN_SYSTEM.md).
* **Telemetria Live dei Processi**: Monitoraggio in tempo reale di RAM (Working Set), tempo di attività (Uptime), thread attivi e PID per ogni applicazione della suite in esecuzione.
* **Integrazione CLI nel PATH di Windows (1-Click)**: Configura con un clic `%LocalAppData%\Programs\NotepadREA` e `NOREA` nel `PATH` utente con broadcast `WM_SETTINGCHANGE`, permettendo di avviare `notepadrea` o `norea` da qualsiasi terminale.
* **Client Aggiornamenti Cloud (GitHub Releases)**: Verifica automatica all'avvio o manuale delle release pubblicate su GitHub, con download streaming e aggiornamento a caldo.
* **Cronologia File Recenti**: Accesso rapido ai file aperti di recente dall'editor direttamente dal menu contestuale del launcher.
* **Integrità & Rollback**: Calcolo e verifica SHA-256 dei binari con manifesto `manifest.json`. Aggiornamento atomico con backup `.prev` e ripristino di emergenza a caldo.

### 3. [WinSCPREA](WinSCPREA/WinSCPREA.csproj) (v1.0.2)
Client SFTP e file manager Windows a due pannelli, ricostruito da zero in C# / WinForms .NET 10 sulla base dell'analisi statica di WinSCP 6.7.1.
* Connessioni SFTP con password o chiave privata SSH e verifica dell'impronta host SHA-256.
* Salvataggio dei siti senza password; trasferimenti ricorsivi e gestione di file e cartelle remoti.
* Harness Python Click basato su [CLI-Anything](WinSCPREA/agent-harness/cli_anything/winscprea/README.md): `winscprea --json site list`, REPL interattiva e comandi per elenco, trasferimento e gestione remota. La logica SFTP resta condivisa con l'app tramite `NoreaCli` e SSH.NET.
* Perimetro iniziale limitato a SFTP. Analisi e limiti: [ANALISI_WINSCP_REA.md](ANALISI_WINSCP_REA.md).

### 4. [NoreaCli & MCP Server](NoreaCli/NoreaCli.csproj) (v2.3.0)
Interfaccia a riga di comando universale e **Server MCP (Model Context Protocol)** conforme alle specifiche 2024-11-05 per collegare la suite ad agenti AI come **Claude Code** e **Google Antigravity**:
* **Comandi CLI nativi**:
  ```powershell
  norea open Program.cs -n 42        # Apre NotepadREA direttamente alla riga 42
  norea diff file1.txt file2.txt     # Lancia il confronto side-by-side
  norea hex payload.bin              # Ispezione esadecimale ed entropia
  norea notify "Build riuscita!"     # Mostra il flyout toast desktop in stile ArtCraft
  norea list --json                  # Telemetria e stato delle app in formato JSON
  norea setup-ai                     # Configurazione automatica 1-click per Claude Code e Antigravity
  ```
* **Strumenti MCP per Agenti AI**:
  * `norea_open_file`: porta in primo piano un file e posiziona il cursore per far visualizzare all'utente il codice modificato in tempo reale.
  * `norea_compare_files`: apre il diff visivo side-by-side prima di completare merge o refactoring.
  * `norea_inspect_hex`: analisi dei file binari ed estrazione entropia di Shannon.
  * `norea_notify`: notifica sul desktop l'avanzamento o la conclusione di task lunghi tramite flyout toast.
  * `norea_list_apps` & `norea_launch_app`: ispezione e controllo dei processi della suite.

### 5. [NOREA.Tests](NOREA.Tests/NOREA.Tests.csproj)
Suite di test automatizzati con **xUnit** (52 test):
* Test di codifica e decodifica `TextCodec` (UTF-8, UTF-16, BOM, euristica binaria, EOL misti e payload da 16 MiB).
* Test per `TextTransformTools` (JSON, XML, Base64, URL, ordinamento, rimozione duplicati).
* Test per algoritmi di diff `DiffEngine` e parser unificato `GitGutter`.
* Test per `NoreaBridge`, configurazione AI isolata in directory temporanee, persistenza e recupero dei file impostazioni/catalogo malformati ed esclusione automatica di benchmark e suite test.
* Test per applicazione aggiornamenti da archivio: download simulato, hash, rollback e rifiuto di percorsi ZIP esterni alla cartella di installazione.
* Test di integrazione del server MCP JSON-RPC su stream, modifiche ai file aperti e conflitti di salvataggio.
* Test di utilità del launcher `LauncherUtil` (split nomi REA, semantica delle versioni `CompareVersions`, anonimizzazione percorsi, telemetria).

---

## 🛡️ Clean-Room Engineering Guarantee

Il codice sorgente di **NotepadREA**, **WinSCPREA** e **NoreaLauncher** è sviluppato **interamente da zero in C# .NET 10**:
1. Nessun codice sorgente o risorsa grafica di Notepad++ o WinSCP è incluso nei sorgenti o nei binari delle ricostruzioni.
2. NotepadREA usa Scintilla5.NET; WinSCPREA usa SSH.NET per SFTP.
3. Le analisi sono documentate in ANALISI_REA.md e ANALISI_WINSCP_REA.md.

---

## 🚀 Compilazione, Test ed Esecuzione

Requisiti: **.NET 10 SDK** (Windows 10 / 11 x64).

```powershell
# Ripristino e compilazione soluzione
dotnet build NOREA.slnx -c Release

# Esecuzione test unitari (52 test)
dotnet test NOREA.Tests\NOREA.Tests.csproj

# Generazione automatica pacchetto portatile (.zip compatto ~3 MB)
.\crea_pacchetto_portatile.ps1

# Pubblicazione manuale binari ad alte prestazioni in dist\
dotnet publish NotepadREA\NotepadREA.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o dist
dotnet publish NoreaLauncher\NoreaLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o dist
dotnet publish WinSCPREA\WinSCPREA.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o dist

# Registrazione associazioni file ed estensioni (.txt, .cs, .py, ecc.)
.\installa_associazioni.ps1
```

### Benchmark I/O file grandi

Il benchmark misura separatamente hash su buffer/file, lettura+decodifica e codifica+scrittura. Riporta la mediana dei tempi e le allocazioni gestite per operazione; non è un test di regressione prestazionale e i risultati dipendono da disco, runtime e dimensione della cache.

```powershell
# Usa 1, 16 e 64 MiB con 3 iterazioni; personalizzabile con dimensioni e iterazioni
dotnet run --project NOREA.Benchmarks\NOREA.Benchmarks.csproj -c Release -- 1,16,64 3
```

### Verifica provenienza del pacchetto di release

Prima di pubblicare una release, la GitHub Action verifica l'attestazione di
provenienza di entrambi gli artefatti. La stessa verifica è disponibile localmente
con GitHub CLI:

```powershell
gh attestation verify NOREA_Suite_Portable.zip --repo chrisquire80/REA
```

Il controllo SHA-256 eseguito dal launcher verifica l'integrità rispetto al digest
pubblicato da GitHub; il launcher non verifica autonomamente l'attestazione. Il
controllo CLI verifica anche il repository e il workflow che hanno prodotto
l'artefatto.
