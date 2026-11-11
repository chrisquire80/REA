# NOREA — Reverse Engineering & Clean-Room Reconstruction Suite

**NOREA** è una suite di ingegneria inversa *clean-room* e ricostruzione nativa ad alte prestazioni in **C# / .NET 10 Windows**, progettata per analizzare software Win32 legacy ed estrarre componenti, architettura e funzionalità in implementazioni moderne, sicure e modulari.

---

## 📦 Progetti della Suite

### 1. [NotepadREA](file:///c:/Users/ChristianRobecchi/Downloads/REA/NotepadREA/NotepadREA.csproj) (v2.0.0)
Editor di testo e codice sorgente completo, ricostruito da zero a partire dall'analisi statica e dinamica di Notepad++ 8.9.8.1 (PE32+ AMD64).
* **Motore Scintilla / Lexilla**: Lexer integrati per C#, C/C++, Python, JS/TS, HTML/CSS, SQL, Markdown, PowerShell, Batch, XML/JSON, INI, Shell.
* **TextCodec puro & Salvataggio Atomico**: Rilevamento automatico codifiche (UTF-8 con/senza BOM, UTF-16 LE/BE, ANSI di sistema), fallback su file temporaneo con `File.Replace`, protezione da sovrascrittura binari accidentale e rilevamento fine linea mista (CRLF/LF).
* **Salvataggio con Privilegi Elevati (UAC Fallback)**: Se il salvataggio fallisce per permessi (es. file di sistema protetti), avvia un worker elevato `NotepadREA.exe --elevated-save <tmp> <dest>`.
* **Find & Replace in Files**: Finestra dedicata multi-threaded (`Ctrl+Shift+F`) con supporto a sottocartelle, regex, case sensitive, navigazione rapida con doppio clic e visualizzazione contesto riga.
* **Macro Engine**: Registratore e riproduttore di macro Scintilla (`SCI_STARTRECORD`, `SCI_STOPRECORD`, `DirectMessage`) con loop di esecuzione multipla.
* **Stampa & Anteprima di Stampa**: Supporto nativo WinForms `PrintDocument` con margini configurabili, numerazione pagine e anteprima interattiva.
* **Logging Strutturato**: Rotazione automatica giornaliera in `%AppData%\NotepadREA\logs\notepadrea-YYYYMMDD.log`.

### 2. [NoreaLauncher](file:///c:/Users/ChristianRobecchi/Downloads/REA/NoreaLauncher/NoreaLauncher.csproj) (v1.0.0)
Launcher della suite in stile **Master-Detail (JetBrains Toolbox / Visual Studio)**.
* **Layout Master-Detail**: Sidebar compatta a sinistra con elenco delle applicazioni della suite; pannello di dettaglio esteso a destra con metadati di runtime, changelog, percorsi binari e schede di analisi statica REA.
* **Integrità & Rollback**: Calcolo e verifica SHA-256 dei binari con manifesto in `%AppData%\NOREA\manifest.json`. Aggiornamento atomico con backup `.prev` e funzione di rollback a caldo.
* **Privacy nei percorsi**: Anonimizzazione automatica dei percorsi utente (`%LocalAppData%`, `%AppData%`, `%UserProfile%`) nei metadati e nelle viste di sistema.
* **Command Palette globale (`Ctrl+K`)**: Avvio rapido delle applicazioni, navigazione file di configurazione e sincronizzazione catalogo.
* **Accessibilità WCAG & Dark Theme**: Superfici a grigio profondo (`#121212`, `#1E1E1E`, `#252525`), badge desaturati (lavanda `#8E8CD8`), etichette di analisi in celeste pastello (`#79C0FF`) ed eliminazione del nero puro (#000000).
* **Integrazione di Sistema**: Supporto System Tray con menu contestuale rapido, avvio al login (`HKCU\...\Run`), arresto controllato dei processi con `CloseMainWindow()` e associazione file Windows.

### 3. [NOREA.Tests](file:///c:/Users/ChristianRobecchi/Downloads/REA/NOREA.Tests/NOREA.Tests.csproj)
Suite di test unitari automatizzati con **xUnit**:
* Test di codifica e decodifica `TextCodec` (UTF-8, UTF-16, BOM, binary heuristics, mixed EOL).
* Test di utilità del launcher `LauncherUtil` (split nomi REA, semantica delle versioni `NormalizeVersion` / `CompareVersions`, anonimizzazione percorsi, hash crittografici SHA-256).

---

## 🛡️ Clean-Room Engineering Guarantee

Il codice sorgente di **NotepadREA** e **NoreaLauncher** è stato sviluppato **interamente da zero in C# .NET 10**:
1. Nessuna riga di codice C++ o GPL proveniente da Notepad++ è inclusa nei sorgenti o nei binari.
2. Il motore di editing si basa sul componente open source pulito **Scintilla5.NET** (licenza Scintilla/Lexilla permissiva).
3. Tutti i metadati di analisi statica e compatibilità sono documentati in [ANALISI_REA.md](file:///c:/Users/ChristianRobecchi/Downloads/REA/ANALISI_REA.md).

---

## 🚀 Compilazione ed Esecuzione

Requisiti: **.NET 10 SDK** (Windows 10 / 11 x64).

```powershell
# Ripristino e compilazione soluzione
dotnet build NOREA.slnx -c Release

# Esecuzione test unitari
dotnet test NOREA.Tests\NOREA.Tests.csproj

# Pubblicazione binari ad alte prestazioni (avvio istantaneo < 400ms)
dotnet publish NotepadREA\NotepadREA.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o dist
dotnet publish NoreaLauncher\NoreaLauncher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o dist

# In alternativa, per bundle completamente autonomi senza .NET installato (evitando compressione per rapidità):
# dotnet publish NotepadREA\NotepadREA.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=false -p:DebugType=none -o dist

# Registrazione associazioni file ed estensioni (.txt, .cs, .py, ecc.)
.\installa_associazioni.ps1
```
