using System.Text.Json;
using EverythingREA;

namespace NoreaCli;

/// <summary>
/// <c>norea search</c> indicizza una cartella e stampa i risultati con la stessa sintassi di EverythingREA.
/// Non avvia i watcher e non scrive la cache dell'app GUI.
/// </summary>
public static class EverythingSearchCli
{
    public static async Task<int> RunAsync(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var roots = new List<string>();
        int limit = 200;
        bool json = false;
        bool matchCase = false;
        bool matchPath = false;
        var queryParts = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a is "-h" or "--help" or "help")
            {
                PrintHelp(stdout);
                return 0;
            }
            if (a is "--json") { json = true; continue; }
            if (a is "--case" or "--match-case") { matchCase = true; continue; }
            if (a is "--path" or "--match-path") { matchPath = true; continue; }
            if (a is "--root" or "-r")
            {
                if (i + 1 >= args.Length)
                {
                    stderr.WriteLine("Errore: --root richiede una cartella.");
                    PrintHelp(stderr);
                    return 1;
                }
                roots.Add(args[++i]);
                continue;
            }
            if (a is "--limit" or "-n")
            {
                if (i + 1 >= args.Length || !int.TryParse(args[++i], out limit) || limit < 1)
                {
                    stderr.WriteLine("Errore: --limit richiede un intero positivo.");
                    return 1;
                }
                continue;
            }
            if (a.StartsWith('-'))
            {
                stderr.WriteLine($"Opzione sconosciuta: {a}");
                PrintHelp(stderr);
                return 1;
            }
            queryParts.Add(a);
        }

        if (queryParts.Count == 0)
        {
            stderr.WriteLine("Errore: specificare la query di ricerca.");
            PrintHelp(stderr);
            return 1;
        }

        if (roots.Count == 0)
            roots.Add(Directory.GetCurrentDirectory());

        var normalized = EverythingSettings.NormalizeRoots(roots);
        if (normalized.Count == 0)
        {
            stderr.WriteLine("Nessuna cartella di indice valida. Usa --root con un percorso esistente.");
            return 1;
        }

        var settings = new EverythingSettings
        {
            IndexedRoots = normalized,
            WatchFileSystem = false,
            IncludeHidden = false
        };

        using var index = new FileIndexService();
        int total = await index.RebuildIndexAsync(settings);
        var hits = index.Search(string.Join(' ', queryParts), new SearchOptions(
            MatchCase: matchCase,
            MatchPath: matchPath,
            MaxResults: limit));

        if (json)
        {
            var payload = hits.Select(entry => new
            {
                entry.Name,
                entry.FullPath,
                entry.Directory,
                entry.SizeBytes,
                entry.ModifiedUtc,
                entry.IsDirectory,
                entry.Extension
            });
            stdout.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
        }
        else
        {
            foreach (var entry in hits)
                stdout.WriteLine(entry.FullPath);
            stderr.WriteLine($"{hits.Count} risultati su {total} elementi indicizzati.");
        }

        return 0;
    }

    static void PrintHelp(TextWriter writer)
    {
        writer.WriteLine("""
            Uso: norea search <query> [--root <cartella>] [--limit <n>] [--case] [--path] [--json]
            Indicizza la cartella (predefinito: directory corrente) e stampa i percorsi trovati.
            La query usa la sintassi di EverythingREA: spazio = AND, | = OR, ! = NOT, wildcard, regex:, ext:, file:, folder:, size:.
            """);
    }
}
