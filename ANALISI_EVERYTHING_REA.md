# Everything 1.4.1.1032: analisi e ricostruzione (EverythingREA)

## 1. Input e metodo

| Elemento | Valore |
|---|---|
| Binario analizzato | `Everything.exe` (`Everything-1.4.1.1032.x64.zip`) |
| Versione | 1.4.1.1032 (x64) |
| Dimensione | 2.272.424 byte |
| SHA-256 | `f191f756996a14a11e5445fa7103d302efd510cf2fbf920e6c0c8ed51d512e36` |
| Formato | PE32+ (`0x020B`), AMD64 (`0x8664`), sottosistema Windows GUI, 6 sezioni |
| Hardening PE | ASLR attivo, DEP/NX attivo, CFG non attivo |
| Firma | Authenticode valida; firmatario `voidtools PTY LTD` |
| Altri file nell'archivio | `Everything.lng` (risorse di localizzazione multilingua) |

La ricognizione locale ha letto i metadati PE, le caratteristiche di sicurezza e la firma Authenticode del binario estratto in area temporanea. Il codice sorgente e le risorse grafiche del prodotto originale non sono stati riutilizzati.

## 2. Comportamento e architettura osservata

Everything è un motore di ricerca locale istantanea per file e cartelle su Windows:
* Mantiene in memoria un indice leggero dei percorsi, dimensioni e timestamp dei file locali.
* Aggiorna i risultati in tempo reale ad ogni carattere digitato (*search-as-you-type*), supportando operatori booleani (`spazio` = AND, `|` = OR, `!` = NOT), wildcard (`*`, `?`), espressioni regolari (`regex:`) e filtri per estensione (`ext:`), tipo (`file:`, `folder:`) e dimensione (`size:>`).
* Utilizza una vista tabellare virtualizzata per visualizzare centinaia di migliaia di risultati senza latenza.

## 3. Ricostruzione: EverythingREA 1.0.0

**EverythingREA** è scritto interamente da zero in **C# .NET 10 WinForms** seguendo il design system **NoreaDesign**, con motore di indicizzazione e ricerca separato in `EverythingREA.Core` (zero dipendenze esterne, nessuna chiamata al binario originale).

| Area | Implementato |
|---|---|
| Motore indice | Scansione parallela multi-thread con `EnumerationOptions` + cache persistente su disco + monitoraggio live `FileSystemWatcher` |
| Sintassi ricerca | AND (`spazio`), OR (`\|`), NOT (`!`), wildcard (`*`, `?`), `regex:`, `ext:`, `file:`, `folder:`, `size:>`, `size:<` |
| Interfaccia GUI | `ListView` virtualizzata ad alte prestazioni (`VirtualMode`), pillole filtro rapide, ordinamento colonne, tema Scuro/Chiaro `NoreaDesign` |
| Integrazione Suite | Apertura diretta dei file in **NotepadREA**, copia percorso, gestione cartelle indicizzate e comando CLI `norea search <query> [--root <cartella>]` (indicizza la cartella indicata, per default quella corrente, senza usare la cache della GUI) |
