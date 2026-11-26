# Changelog

Generato dai messaggi di commit fra i tag `v*`. Il workflow di release aggiorna questo file e usa la sezione del tag come note della release.

## Non rilasciato

- feat(launcher): document process telemetry and add an opt-out
- test: cover editor, SFTP, search, and catalog decisions outside the forms
- ci: move Actions to Node 24 and keep only win-x64 Scintilla natives
- fix(release): pass the tag commit SHA as target_commitish
- ci: publish coverage, run the harness, and check the package
- refactor: share NoreaDesign and split the large forms
- docs: document EverythingREA and the real norea commands
- build: centralize suite version on 2.5.0
- chore: drop machine-specific PE analysis dumps
- fix(installer): find NotepadREA.exe in repo and portable layouts
- docs: add the MIT license for NOREA
- fix(release): skip provenance attestation on user-owned private repos

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
