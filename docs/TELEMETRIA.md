# Telemetria del launcher

NOREA non ha un servizio di analytics. Non esiste un identificatore di macchina, un account e un endpoint diverso da GitHub a cui il launcher invii statistiche.

## Cosa resta sul PC

| Dato | Dove si legge | Dove finisce |
|---|---|---|
| Working set (MB), ora di avvio (da cui si calcola l'uptime), numero di thread, PID | `Process` di Windows, solo per le app della suite già riconosciute come in esecuzione | Scheda del dettaglio nel launcher. Non viene scritto in un file e non viene messo in una richiesta HTTP |
| Percorso dell'eseguibile mostrato nel dettaglio | Disco locale | In interfaccia il prefisso del profilo utente è sostituito con `%UserProfile%`, `%AppData%` o `%LocalAppData%` (`LauncherUtil.AnonymizePath`). È solo testo a schermo |
| `norea list` e lo strumento MCP `norea_list_apps` | Processo locale e versione del file | Solo lo stdout o la risposta MCP sullo stesso PC: id, nome, versione, se è installata, percorso, se è in esecuzione, PID. Non includono RAM, uptime o thread |
| Impostazioni | `%AppData%\NOREA\launcher_settings.json` | Solo quel file. Contiene preferenze (tema, cartella di installazione, repository GitHub configurato, il flag di telemetria) |
| Log di diagnosi | `%AppData%\NOREA\logs\launcher-yyyyMMdd.log` (e le cartelle log delle altre app) | Solo disco locale. Servono quando un aggiornamento o un salvataggio fallisce. Il flag di telemetria non li spegne, altrimenti non resterebbe traccia di un errore |

## L'unica richiesta di rete

Se **Verifica aggiornamenti cloud all'avvio** è attivo (è il comportamento predefinito, perché l'aggiornamento è una funzione della suite), all'avvio il launcher esegue:

`GET https://api.github.com/repos/{repository}/releases`

Header inviati:

- `User-Agent: NoreaLauncher-CloudUpdate/1.0`
- `Accept: application/vnd.github.v3+json`

Non c'è corpo. GitHub vede l'indirizzo IP della macchina e quello user-agent, non i percorsi dei file e non le metriche di processo. Il repository predefinito è `chrisquire80/REA` e si cambia nelle impostazioni.

Se c'è una release più nuova e il pacchetto ZIP ha un digest `sha256:`, il launcher scarica lo zip da `https://github.com/...`. Anche questo non è un invio di telemetria: è il download dell'aggiornamento.

Il controllo manuale **Verifica ora** fa la stessa richiesta anche se il controllo all'avvio è spento. È un'azione esplicita.

## Opt-out

In **Impostazioni → Privacy e telemetria** la casella **Mostra RAM, tempo di attività e thread** è la disattivazione delle metriche di processo.

- È **attiva per impostazione predefinita**. I numeri non escono dal PC e la scheda è una funzione già presente: spegnerla di default toglierebbe quella scheda a chi aggiorna la suite. Il controllo aggiornamenti resta quello già esistente, perché spegnerlo di default toglierebbe gli aggiornamenti automatici.
- Se la casella è spenta, `LauncherUtil.GetProcessTelemetry` non apre il processo. Il dettaglio mostra solo «In esecuzione», senza RAM, uptime, thread e PID. Il pulsante Avvia/Arresta continua a funzionare: sapere se il programma è aperto serve a chiuderlo.
- La preferenza è `CollectProcessTelemetry` in `launcher_settings.json`. Un file vecchio, senza la proprietà, resta con la telemetria attiva.

Per non contattare GitHub all'avvio, spegnere **Verifica aggiornamenti cloud all'avvio** nella scheda aggiornamenti.
