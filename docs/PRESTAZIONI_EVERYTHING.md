# Prestazioni di EverythingREA

Il README parla di avvio della suite sotto i 400 ms. Quella cifra non è una misura dell'indice. Qui si misurano indicizzazione e ricerca di `EverythingREA.Core` su un albero sintetico: 100 file di testo per cartella, nomi `bucket-0000/file-0000.txt`, senza `FileSystemWatcher`.

Comando (la prima modalità, senza `everything`, resta il benchmark di I/O di NotepadREA):

```powershell
dotnet run --project NOREA.Benchmarks\NOREA.Benchmarks.csproj -c Release -- everything 20000 3
```

Scrive `everything-benchmark.csv` e le stesse righe sulla console. La mediana è l'elemento centrale dopo l'ordinamento, quindi con 3 iterazioni include anche la prima, più lenta.

## Misura locale

Macchina di sviluppo Linux x64, .NET SDK 10.0.404, disco temporaneo locale. Query di ricerca: `file-0001 ext:txt`.

| File richiesti | Elementi indicizzati | Mediana indice | Mediana ricerca |
|---:|---:|---:|---:|
| 8 000 | 8 080 | 19,4 ms | 2,4 ms |
| 20 000 | 20 200 | 61,8 ms | 4,8 ms |

Gli 80 e 200 in più sono le cartelle. Su questo albero sia l'indice da 20 000 file sia la ricerca stanno sotto i 400 ms. Non è il tempo di avvio della finestra WinForms e non va letto come garanzia su un disco di sistema pieno di file reali: l'indice visita il filesystem, quindi un albero con file grandi o un antivirus può allungarlo. La ricerca, a indice già in memoria, resta un filtro sulla lista.

## CI

`.github/workflows/benchmarks.yml` gira su `windows-latest` a ogni pull request e su `main`. Usa 2 000 file e 2 iterazioni, pubblica `everything-benchmark.csv` come artefatto e nel riepilogo del job.

Il job fallisce se la mediana dell'indice supera 4 000 ms o quella della ricerca supera 800 ms. Sono tetti larghi, scelti per segnalare una regressione grossa (per esempio un algoritmo diventato quadratico) senza far fallire il job per il rumore di un runner. Non sono l'obiettivo di prestazione: l'obiettivo documentato è la tabella sopra.
