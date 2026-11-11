# Notepad++ 8.9.8.1 Portable: analisi con REA e ricostruzione (NotepadREA)

## 1. Input

| Elemento | Valore |
|---|---|
| File | `notepad/NotepadPlusPlusPortable_8.9.8.1.paf.exe` (11,4 MB, installer PortableApps / NSIS) |
| Estrazione | `7z x`, in `extracted/` (356 file) |
| Binario principale | `extracted/App/Notepad++64/notepad++.exe` |
| SHA-256 | `c52c9ed2ec6d0b5b3f243b29f2124f98db56b6ba92bb42a9af2665709464b3cf` |
| Dimensione | 8.546.288 byte |
| Strumento | REA `rea-agents@6.8.0` (CLI, analisi statica, nessun motore nativo) |

Le prove JSON di REA sono in `analysis/` (`inspect-artifact`, `inspect-pe-imports`, `inspect-pe-resources`,
`inspect-pe-certificates`, `inspect-pe-load-config`). `inspect-binary-layout` non è disponibile su Windows:
il profilo pwntools di REA supporta solo Linux x64.

## 2. Cosa è emerso (dati di REA)

**Formato:** PE32+, AMD64, firmato Authenticode (PKCS#7 SHA-256). La catena di firma è
GlobalSign Code Signing Root R45, poi GlobalSign GCC R45 CodeSigning CA 2020, poi `CN=NOTEPAD++` (Parigi, FR).

**Hardening (`inspect-pe-load-config`):** security cookie (/GS) e Control Flow Guard (puntatori check/dispatch
presenti). Esiste una directory TLS ma non registra callback.

**Importazioni (`inspect-pe-imports`):** 21 DLL, tutte Win32 nativo. Non c'è nessun runtime (.NET, Qt, Electron).

| DLL | Funzioni | Ruolo dedotto |
|---|---|---|
| USER32 | 216 | finestre, menu, messaggi, acceleratori |
| KERNEL32 | 195 | file, memoria, processi |
| GDI32 | 66 | disegno, font |
| ADVAPI32 | 26 | registro, sicurezza |
| COMCTL32 | 24 | tab, toolbar, status bar, list view |
| SHLWAPI / SHELL32 | 18 / 12 | percorsi, drag&drop, dialog shell |
| UxTheme / dwmapi | 16 / 2 | temi visivi, modalità scura |
| ole32 / OLEAUT32 | 12 / 2 | COM (dialog file moderni) |
| IMM32 | 9 | input IME (lingue asiatiche) |
| CRYPT32 / WINTRUST / bcrypt | 8 / 1 / 7 | verifica firme dei plugin, hash MD5/SHA |
| WININET / SensApi | 1 / 2 | controllo aggiornamenti, connettività |
| COMDLG32, VERSION, dbghelp, MSIMG32 | – | dialog comuni, versione, minidump, alpha blend |

**Motore di editing:** nessuna `SciLexer.dll` importata. Le stringhe RTTI `.?AV…Scintilla@@` e `Lexilla@@` nel
binario indicano che **Scintilla e Lexilla sono linkati staticamente**: il componente editor è Scintilla.

**Risorse (`inspect-pe-resources`, 2.487 voci):**

| Tipo | N. |
|---|---|
| RT_ICON (3)* | 2.067 |
| RT_GROUP_ICON (14) | 302 |
| RT_DIALOG (5) | 70 |
| RT_BITMAP (2) | 35 |
| RT_MENU (4) | 3 (menu principale, menu schede, menu tray) |
| RT_CURSOR / VERSION / MANIFEST | 4 / 1 / 1 |

\*Le singole immagini dei 302 gruppi di icone (toolbar standard, Fluent, scura). Non ci sono RT_STRING né
RT_ACCELERATOR: testi e scorciatoie sono caricati da XML a runtime.

REA non decodifica i payload diversi da RT_GROUP_ICON. Per questo la struttura dei menu viene da
`localization/english.xml` (339 comandi in 12 menu: File, Edit, Search, View, Encoding, Language, Settings,
Tools, Macro, Run, Plugins, Window) e da `shortcuts.xml`. La configurazione è in XML: `config.xml`,
`langs.model.xml`, `stylers.model.xml`, `contextMenu.xml`. I plugin inclusi sono mimeTools, NppConverter e NppExport.

## 3. Ricostruzione: NotepadREA

Il codice è scritto da zero in C# / WinForms .NET 10 e non contiene codice di Notepad++. Per l'editing usa
**Scintilla + Lexilla** (NuGet `Scintilla5.NET` 7.1.1), cioè lo stesso motore identificato nel binario.

| Area | Implementato |
|---|---|
| File | nuovo, apri (multiplo), ricarica, salva, salva con nome, salva copia, salva tutto, rinomina, chiudi / tutti / tutti tranne corrente / non modificati, ripristina ultimo chiuso, file recenti, drag&drop, argomenti da riga di comando, ripristino sessione |
| Schede | una scheda per documento, `*` sui file modificati, chiusura con click centrale, menu contestuale (chiudi a sinistra/destra, copia percorso, apri cartella, sola lettura), Ctrl+Tab |
| Modifica | annulla/ripeti, duplica/elimina/sposta righe, unisci righe, ordinamenti (lessicale, ignorando maiuscole, interi, lunghezza, inverso, casuale), rimozione duplicati e righe vuote, maiuscole/minuscole/iniziali/frase/inverti, operazioni sugli spazi, tab↔spazi, commento di riga (Ctrl+Q) e di blocco, data/ora, sola lettura, multi-selezione e selezione rettangolare (Alt+trascina) |
| Cerca | Trova/Sostituisci non modale (maiuscole, parola intera, regex C++11, ricerca circolare, solo nella selezione, conta, evidenzia tutto, sostituisci tutto), F3/Maiusc+F3, Ctrl+F3 sulla parola corrente, Vai alla riga, parentesi corrispondente, segnalibri (Ctrl+F2, F2, Maiusc+F2) |
| Vista | a capo automatico, spazi/EOL visibili, guide di rientro, numeri di riga, zoom, piegatura del codice (Alt+0 / Alt+Maiusc+0), schermo intero (F11), sempre in primo piano, tema chiaro/scuro, scelta carattere |
| Codifica | rilevamento ANSI / UTF-8 / UTF-8-BOM / UTF-16 LE/BE, conversione, reinterpretazione, conversione EOL CRLF/LF/CR |
| Linguaggi | testo, C, C++, C#, Java, JS/TS, JSON, Python, HTML, XML, CSS, SQL, Batch, PowerShell, INI, YAML, Markdown, Bash, scelti automaticamente dall'estensione |
| Barra di stato | linguaggio, lunghezza/righe, Rg/Col/Sel, EOL, codifica, INS/SSC |
| Strumenti | hash MD5/SHA-256, statistiche del documento |
| Rilevamento modifiche | se il file cambia su disco mentre la finestra non è attiva, propone di ricaricarlo |

Le scorciatoie da tastiera seguono quelle predefinite di Notepad++.

**Non implementato:** macro, Run, plugin, stampa, vista divisa e doppia vista, Function List, Folder as Workspace,
User Defined Language, auto-completamento, localizzazioni.

## 3b. Versione 2.0: interfaccia e produttività

| Area | Novità |
|---|---|
| Temi | 4 temi completi (Chiaro, Scuro, Monokai, Solarized) applicati a editor, menu, menu contestuali, toolbar, schede, barra di stato, barra Trova e barra del titolo (Ctrl+Maiusc+K per passare al successivo) |
| Icone | Toolbar con glifi vettoriali Segoe Fluent Icons (Segoe MDL2 su Win10), ridisegnati per DPI e colore del tema. Gli interruttori attivi (a capo, simboli, minimappa) sono evidenziati |
| Schede | Barra disegnata da zero: × su scheda attiva o sotto il mouse, pallino colorato sui documenti non salvati, riordino trascinando, clic centrale per chiudere, rotella per scorrere, doppio clic sullo spazio vuoto per un nuovo documento, menu contestuale esteso |
| Editor | Linea di separazione tra margine e testo; evidenziazione automatica di tutte le occorrenze della parola selezionata; Alt+Su/Giù sposta le righe; Ctrl+Maiusc+D aggiunge un cursore sull'occorrenza successiva |
| Minimappa | Vista laterale del documento con l'area visibile evidenziata; si usa con clic, trascinamento o rotella (Ctrl+Maiusc+M per mostrarla o nasconderla) |
| Trova/Sostituisci | Barra ancorata in basso al posto della finestra: ricerca mentre si scrive, conteggio dei risultati in tempo reale, opzioni maiuscole, parola intera, regex, evidenzia tutto e solo nella selezione; avvisa se la regex non è valida |
| Apertura rapida | Ctrl+P cerca tra file aperti, file recenti e file della cartella corrente (fino a 2 livelli, esclusi .git e node_modules); `:123` va alla riga 123 |
| Recupero sessione | Ogni 7 s i buffer non salvati, anche quelli senza nome, vengono copiati in `%APPDATA%\NotepadREA\backup`. Alla chiusura l'editor non chiede di salvare e al riavvio (anche dopo un crash) riapre tutto. Si può disattivare da Impostazioni |
| File esterni | Quando la finestra torna attiva, e ogni 7 s mentre è attiva, controlla se un file è stato modificato o eliminato da un altro programma. Per un file eliminato chiede se tenerlo aperto come non salvato |
| Istanza singola | Come Notepad++: se l'editor è già aperto, i file aperti da Esplora risorse o da riga di comando arrivano alla finestra esistente (mutex + named pipe per utente) |

Provato dall'interfaccia sull'exe compilato: tema Monokai, evidenziazione delle occorrenze, pallino sulle schede non
salvate, barra Trova con conteggio, avviso di file eliminato, recupero dopo chiusura forzata, Ctrl+P,
trascinamento delle schede, Alt+Su, istanza singola.

## 4. Build ed esecuzione

```
cd NotepadREA
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o ..\dist
```

Il risultato è `dist/NotepadREA.exe` (≈54 MB, autonomo: non serve .NET installato). Le impostazioni e la sessione
sono salvate in `%APPDATA%\NotepadREA\settings.json`.

## 5. Verifiche fatte

- Apertura di un file `.cs`: evidenziazione della sintassi C#, piegatura, numeri di riga, BOM UTF-8 rilevato.
- Duplica riga, ordinamento A→Z, Sostituisci tutto (3 occorrenze), tema scuro: provati dall'interfaccia.
- Il file salvato mantiene il BOM.
- L'exe single-file pubblicato si avvia correttamente. Con `IncludeNativeLibrariesForSelfExtract` andava in errore
  perché Scintilla.NET cerca le DLL in `runtimes\win-x64\native`; si risolve con `IncludeAllContentForSelfExtract`.
