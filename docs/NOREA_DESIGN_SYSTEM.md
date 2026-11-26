# Sistema di design NOREA

Questo documento definisce l'aspetto e i comportamenti comuni dei programmi NOREA. I token per le app Windows Forms sono implementati nel progetto `NoreaDesign` (`NoreaDesign/NoreaDesign.csproj`), referenziato da NoreaLauncher, NotepadREA, WinSCPREA e EverythingREA.

## Direzione visiva

- Interfacce sobrie e leggibili, con una gerarchia chiara tra titolo, contenuto e azione principale.
- Blu NOREA per l'azione principale e i collegamenti; corallo solo per identità, attenzione e azioni distruttive.
- Superfici piatte, bordi sottili e separazioni coerenti. Evitare ombre marcate, effetti luminosi, gradienti decorativi e animazioni non necessarie.
- Preferire verbi brevi e specifici per pulsanti ed etichette. La lingua predefinita dell'interfaccia è l'italiano.

## Palette

Le app seguono il tema di Windows, salvo un'impostazione esplicita dell'app. `NoreaStyles.Dark` e `NoreaStyles.Light` sono la fonte dei colori per i nuovi programmi Windows Forms.

| Token | Scuro | Chiaro | Uso |
|---|---|---|---|
| Canvas | `#101722` | `#F3F6FA` | Sfondo principale |
| Header | `#121D2B` | `#FFFFFF` | Barra superiore e stato |
| Pannello | `#141F2E` | `#EFF3F8` | Navigazione e intestazioni tabella |
| Superficie | `#1B2939` | `#FFFFFF` | Schede, modali, righe |
| Hover | `#233348` | `#F5F8FC` | Evidenziazione al passaggio |
| Selezione | `#203A58` | `#E7F1FF` | Riga o elemento selezionato |
| Campo | `#111B29` | `#FFFFFF` | Input e ricerca |
| Bordo | `#2B3B50` | `#D9E1EA` | Separatore sottile |
| Testo principale | `#F2F6FB` | `#172333` | Titoli e valori |
| Testo secondario | `#BDCAD9` | `#47576A` | Descrizioni e controlli |
| Testo attenuato | `#91A3B8` | `#64788E` | Metadati e stato neutro |
| Accento blu | `#6DA9FF` | `#205FC0` | Icone, link e dettagli attivi |
| Pulsante principale | `#2E6DC9` | `#205FC0` | Una sola azione primaria per area |
| Successo | `#55C99A` | `#167C57` | Operazioni concluse e stato connesso |
| Attenzione | `#F3B85E` | `#945A00` | Avvisi non bloccanti |
| Pericolo | `#B33830` | `#A32D26` | Eliminazioni, arresti e errori |

Usare colori di stato insieme a una descrizione testuale o a un'icona: il colore da solo non comunica lo stato.

## Tipografia e spaziatura

- Interfaccia: Segoe UI. Dati tecnici e percorsi: Consolas.
- Corpo 9,5 pt; sezioni 10,5 pt; titoli di finestra o schermata 16 pt. Usare il grassetto per la gerarchia, non il maiuscolo esteso.
- Spaziatura basata su multipli di 4 px: 4, 8, 12, 16, 24 e 32 px.
- Controlli interattivi alti almeno 34 px; righe di tabella almeno 34 px e intestazioni almeno 38 px.
- La finestra principale di un'app desktop parte da circa 1280 × 790 px e resta utilizzabile da 960 × 620 px.

## Componenti e interazioni

- Ogni schermata ha una sola azione primaria evidente; le azioni secondarie usano superfici neutre.
- Campi di testo, menu e tabelle condividono sfondi, bordi e stati di selezione del tema attivo.
- Le tabelle allineano numeri e dimensioni a destra, mantengono intestazioni leggibili e mostrano chiaramente la riga attiva.
- Le finestre di dialogo usano lo stesso tema, allineamento e altezza dei controlli della finestra principale.
- Gli errori appaiono vicino all'azione o all'elemento interessato. Le azioni distruttive richiedono una conferma esplicita.
- Tutte le funzioni restano accessibili da tastiera; i pulsanti solo icona hanno `AccessibleName` e tooltip.
- Nessun movimento è necessario per capire lo stato. Se un'animazione viene introdotta, deve essere breve e non contenere informazioni essenziali.

## Icone e identità

- Ogni programma ha un `app.ico` originale e riconoscibile anche a 16 px; la forma NOREA comune usa il blu e un dettaglio corallo.
- Il launcher disegna i pittogrammi delle app con la stessa famiglia di colori e li abbina a nome, versione e stato.
- Preferire icone vettoriali semplici con un significato diretto. Evitare emoji come icone dei comandi.

## Riutilizzo in nuovi programmi Windows Forms

Aggiungere il progetto condiviso alla soluzione e referenziarlo:

```xml
<ItemGroup>
  <ProjectReference Include="..\NoreaDesign\NoreaDesign.csproj" />
</ItemGroup>
```

`LauncherTheme` nel launcher è solo una vista sulle stesse proprietà di `NoreaPalette`: i colori non sono duplicati. I temi dell'editor in `NotepadREA/Theme.cs` (Light, Dark, Monokai, Solarized) restano distinti, perché sono i colori di sintassi Scintilla e non la palette chrome. Condividono la title bar tramite `NoreaStyles.ApplyTitleBar`.

Il log su file è `NoreaLog`. NotepadREA e NoreaLauncher lo avvolgono con la classe `Log` e mantengono i percorsi già usati: `%AppData%\NotepadREA\logs\notepadrea-yyyyMMdd.log` e `%AppData%\NOREA\logs\launcher-yyyyMMdd.log`.

Poi impostare il tema e applicare gli helper ai controlli:

```csharp
using NoreaDesign;

NoreaPalette palette = NoreaDesign.NoreaStyles.CurrentPalette;
NoreaDesign.NoreaStyles.ApplyForm(this, palette);
NoreaDesign.NoreaStyles.StyleInput(searchBox, palette);
NoreaDesign.NoreaStyles.StyleButton(saveButton, palette, NoreaButtonStyle.Primary);
NoreaDesign.NoreaStyles.StyleGrid(resultsGrid, palette);
```

I programmi con un tema selezionabile possono usare `NoreaStyles.Dark` o `NoreaStyles.Light` e salvare la scelta nelle proprie impostazioni. Le app WPF o web devono mantenere gli stessi token, le stesse gerarchie e gli stessi stati, usando i componenti nativi del loro framework.
