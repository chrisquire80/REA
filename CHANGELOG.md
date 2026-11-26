# Changelog

Generato dai messaggi di commit fra i tag `v*`. Se esiste `docs/release-notes/<tag>.md`, quella sezione usa quel testo. Il workflow di release aggiorna questo file e usa la sezione del tag come note della release.

## v2.6.1

- EverythingREA: la ricerca copia l'indice sotto lock e non dereferenzia più campi nulli. Dopo la ricostruzione un watcher poteva passare un elemento nullo a CompileQuery e chiudere l'app con NullReferenceException.
- NoreaLauncher: il repository salvato `ChristianRobecchi/REA` (vecchio default del client aggiornamenti, HTTP 404) viene corretto in `chrisquire80/REA`. Se il repository configurato risponde ancora 404, la barra degli aggiornamenti lo segnala invece di lasciare solo un avviso nel log.

## v2.6.0

- Versione di suite 2.6.0: firma Authenticode opzionale, note di rilascio generate dai tag, installer per utente, MSIX e manifest winget, benchmark di EverythingREA, telemetria con opt-out e test della logica delle finestre.

## v2.5.0

- feat: add EverythingREA instant file search engine and launcher integration

## v2.4.1

- fix(launcher): exclude library/core projects from sidebar and fix scrolled usage guide text rendering

## v2.4.0

- test: add xUnit coverage for WinSCPREA.Core and WinScpAgentCli
- feat: add WinSCPREA SFTP client, NoreaDesign system, and CLI/agent harness
- fix(launcher): exclude benchmark tools from catalog and clean test conflicts

## v2.3.0

- feat: add NOREA.Benchmarks project, release attestations verification, and 51 unit tests
- fix(cli): prevent test runners from overwriting norea.exe in local user path
- Agent Host changes for agents/progetto-analisi (#1)
- Agent Host changes for agents/progetto-analisi
- fix: exclude cli/mcp tools (norea, noreacli) from user app catalog
- feat: add NoreaCli universal command line and native MCP server for Claude Code & Antigravity

## v2.2.0

- feat: add modern dark update notification flyout in ArtCraft style
- fix: display dynamic launcher version v2.2.0 in title bar and settings
- fix: bump suite version to v2.2.0 and dynamically resolve app version from binary metadata
- feat: add embedded powershell terminal, git gutter diff, hex viewer, and rich github showcase

## v2.1.0

- chore: set default GitHub repo to chrisquire80/REA
- feat: implement GitHub Cloud Releases distribution and auto-update client in NoreaLauncher
- feat: add portable side-by-side detection and packaging script crea_pacchetto_portatile.ps1
- feat: add side-by-side file compare, text transforms suite, CLI PATH integration, and live process telemetry
- fix: embed native Scintilla/Lexilla libraries and fix working directory in launcher
- docs: update publish instructions for high-performance instant startup
- perf: enable ReadyToRun & TieredPGO, parallelize FindInFiles, optimize TextCodec, cache GDI fonts, and streamline event-driven process tracking
- feat: complete overhaul of NotepadREA and NoreaLauncher with tests, master-detail UI, and icon fixes
- chore: stato iniziale NotepadREA + NoreaLauncher
