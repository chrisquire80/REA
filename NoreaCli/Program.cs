using System.Text.Json;

namespace NoreaCli;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        string cmd = args[0].ToLowerInvariant();

        try
        {
            switch (cmd)
            {
                case "mcp":
                    await McpServer.RunAsync();
                    return 0;

                case "setup-ai":
                    Console.WriteLine("=== Configurazione AI Bridge (Claude Code & Antigravity) ===");
                    var (ok, msg) = AiSetup.RunSetup();
                    Console.WriteLine(msg);
                    return ok ? 0 : 1;

                case "open":
                    return HandleOpen(args[1..]);

                case "diff":
                    return HandleDiff(args[1..]);

                case "hex":
                    return HandleHex(args[1..]);

                case "notify":
                    return HandleNotify(args[1..]);

                case "list":
                    return HandleList(args[1..]);

                case "launch":
                    return HandleLaunch(args[1..]);

                case "status":
                    return HandleStatus();

                case "winscp":
                    return await WinScpAgentCli.RunAsync(args[1..]);

                case "search":
                    return await EverythingSearchCli.RunAsync(args[1..]);

                default:
                    // Se l'argomento è un file esistente, aprilo direttamente in NotepadREA
                    if (File.Exists(args[0]))
                    {
                        return HandleOpen(args);
                    }
                    Console.Error.WriteLine($"Comando sconosciuto: '{args[0]}'. Esegui 'norea --help' per la lista dei comandi.");
                    return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Errore: {ex.Message}");
            return 1;
        }
    }

    static int HandleOpen(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: norea open <percorso_file> [-n <riga>] [-c <colonna>]");
            return 1;
        }

        string? file = null;
        int line = -1;
        int col = -1;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a.Equals("-n", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out line);
            }
            else if (a.StartsWith("-n", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(a[2..], out line);
            }
            else if (a.Equals("-c", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                int.TryParse(args[++i], out col);
            }
            else if (a.StartsWith("-c", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(a[2..], out col);
            }
            else if (file == null && !a.StartsWith('-'))
            {
                file = a;
            }
        }

        if (file == null)
        {
            Console.Error.WriteLine("Errore: specificare il percorso del file.");
            return 1;
        }

        bool success = NoreaBridge.OpenInNotepad(file, line, col);
        if (success)
        {
            Console.WriteLine($"Aperto in NotepadREA: {file}" + (line > 0 ? $" (riga {line})" : ""));
            return 0;
        }
        Console.Error.WriteLine($"Impossibile aprire {file}. Assicurati che NotepadREA sia installato.");
        return 1;
    }

    static int HandleDiff(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Uso: norea diff <fileA> <fileB>");
            return 1;
        }

        bool success = NoreaBridge.DiffInNotepad(args[0], args[1]);
        if (success)
        {
            Console.WriteLine($"Confronto side-by-side avviato: {args[0]} ⇄ {args[1]}");
            return 0;
        }
        Console.Error.WriteLine("Impossibile avviare il confronto.");
        return 1;
    }

    static int HandleHex(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: norea hex <percorso_file>");
            return 1;
        }

        bool success = NoreaBridge.HexInNotepad(args[0]);
        if (success)
        {
            Console.WriteLine($"Ispezione esadecimale aperta per: {args[0]}");
            return 0;
        }
        Console.Error.WriteLine("Impossibile aprire la vista esadecimale.");
        return 1;
    }

    static int HandleNotify(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: norea notify <app/titolo> [messaggio] [--old <v>] [--new <v>]");
            return 1;
        }

        string app = args[0];
        string msg = args.Length > 1 && !args[1].StartsWith('-') ? args[1] : "Operazione completata con successo!";
        string oldVer = "v2.1.0";
        string newVer = "v2.2.0";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--old" && i + 1 < args.Length) oldVer = args[++i];
            if (args[i] == "--new" && i + 1 < args.Length) newVer = args[++i];
        }

        bool success = NoreaBridge.NotifyLauncher(app, msg, oldVer, newVer);
        if (success)
        {
            Console.WriteLine($"Notifica desktop inviata: [{app}] {msg}");
            return 0;
        }
        Console.Error.WriteLine("Impossibile mostrare la notifica desktop.");
        return 1;
    }

    static int HandleList(string[] args)
    {
        bool json = args.Any(a => a.Equals("--json", StringComparison.OrdinalIgnoreCase));
        bool runningOnly = args.Any(a => a.Equals("--running", StringComparison.OrdinalIgnoreCase));

        var apps = NoreaBridge.GetAppsStatus(runningOnly);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        Console.WriteLine($"{"APPLICAZIONE",-16} {"VERSIONE",-10} {"STATO",-16} {"PID",-8} {"ESEGUIBILE"}");
        Console.WriteLine(new string('-', 80));
        foreach (var a in apps)
        {
            string name = a["name"].ToString() ?? "";
            string ver = a["version"].ToString() ?? "";
            bool running = (bool)a["isRunning"];
            string status = running ? "● In esecuzione" : "○ Fermo";
            int pid = (int)a["pid"];
            string path = a["executablePath"].ToString() ?? "";

            Console.WriteLine($"{name,-16} {ver,-10} {status,-16} {(running ? pid.ToString() : "-"),-8} {path}");
        }
        return 0;
    }

    static int HandleLaunch(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Uso: norea launch <appId> (es. NotepadREA, NoreaLauncher)");
            return 1;
        }

        bool success = NoreaBridge.LaunchApp(args[0]);
        if (success)
        {
            Console.WriteLine($"Applicazione {args[0]} avviata.");
            return 0;
        }
        Console.Error.WriteLine($"Impossibile trovare o avviare {args[0]}.");
        return 1;
    }

    static int HandleStatus()
    {
        Console.WriteLine("=== NOREA Suite Status ===");
        var apps = NoreaBridge.GetAppsStatus();
        foreach (var a in apps)
        {
            string name = a["name"].ToString() ?? "";
            string ver = a["version"].ToString() ?? "";
            bool running = (bool)a["isRunning"];
            string path = a["executablePath"].ToString() ?? "";
            Console.WriteLine($"• {name} ({ver}): {(running ? "ATTIVO" : "INATTIVO")} - {path}");
        }
        return 0;
    }

    static string CliVersion => typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "2.5.0";

    static void PrintUsage()
    {
        Console.WriteLine($@"NOREA Suite CLI & AI Agent Bridge (v{CliVersion})
Uso: norea <comando> [argomenti...]

Comandi Editor:
  open <file> [-n <line>] [-c <col>]  Apre il file in NotepadREA alla riga/colonna indicata
  diff <file1> <file2>                Apre il confronto side-by-side diff in NotepadREA
  hex <file>                          Apre l'ispezione esadecimale virtualizzata con entropia

Comandi Suite & Notifiche:
  notify <app> [msg] [--old v] [--new v]  Mostra la notifica desktop toast flyout
  list [--json] [--running]                Elenca le applicazioni NOREA e i processi attivi
  launch <appId>                           Avvia un'applicazione (es. NotepadREA, WinSCPREA, EverythingREA)
  status                                   Stato generale della suite
  search <query> [--root <dir>] [--limit <n>] [--json]
                                           Cerca file con la sintassi di EverythingREA

Gestione SFTP agent-native:
  winscp <comando> [--json]               Siti, elenco remoto e trasferimenti SFTP

Integrazione AI (Claude Code & Antigravity):
  mcp                                      Avvia il server MCP JSON-RPC 2.0 su stdio
  setup-ai                                 Configura automaticamente Claude Code e Antigravity
");
    }
}
