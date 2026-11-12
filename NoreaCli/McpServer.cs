using System.Text.Json;
using System.Text.Json.Nodes;

namespace NoreaCli;

/// <summary>
/// Server MCP (Model Context Protocol) JSON-RPC 2.0 su stdio.
/// Permette a Claude Code, Antigravity e qualsiasi client MCP di pilotare la suite NOREA.
/// </summary>
public static class McpServer
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task RunAsync()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;

        using var reader = new StreamReader(Console.OpenStandardInput(), System.Text.Encoding.UTF8);
        using var writer = new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true };

        while (true)
        {
            string? line = await reader.ReadLineAsync();
            if (line == null) break;
            line = line.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            try
            {
                var request = JsonNode.Parse(line)?.AsObject();
                if (request == null) continue;

                var id = request["id"];
                string method = request["method"]?.GetValue<string>() ?? "";

                // Ignora notifiche senza id
                if (id == null)
                {
                    continue;
                }

                var response = HandleRequest(method, request["params"]?.AsObject(), id);
                if (response != null)
                {
                    await writer.WriteLineAsync(response.ToJsonString(JsonOpts));
                }
            }
            catch (Exception ex)
            {
                var errObj = new JsonObject
                {
                    ["jsonrpc"] = "2.0",
                    ["id"] = null,
                    ["error"] = new JsonObject
                    {
                        ["code"] = -32603,
                        ["message"] = ex.Message
                    }
                };
                await writer.WriteLineAsync(errObj.ToJsonString(JsonOpts));
            }
        }
    }

    private static JsonObject? HandleRequest(string method, JsonObject? prms, JsonNode? id)
    {
        return method switch
        {
            "initialize" => HandleInitialize(id),
            "ping" => MakeSuccessResponse(id, new JsonObject()),
            "tools/list" => HandleToolsList(id),
            "tools/call" => HandleToolsCall(prms, id),
            _ => MakeErrorResponse(id, -32601, $"Metodo non trovato: {method}")
        };
    }

    private static JsonObject HandleInitialize(JsonNode? id)
    {
        var result = new JsonObject
        {
            ["protocolVersion"] = "2024-11-05",
            ["capabilities"] = new JsonObject
            {
                ["tools"] = new JsonObject
                {
                    ["listChanged"] = false
                }
            },
            ["serverInfo"] = new JsonObject
            {
                ["name"] = "norea-mcp",
                ["version"] = "2.2.0"
            }
        };
        return MakeSuccessResponse(id, result);
    }

    private static JsonObject HandleToolsList(JsonNode? id)
    {
        var tools = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "norea_open_file",
                ["description"] = "Apre un file istantaneamente in NotepadREA portandolo in primo piano e posizionando il cursore alla riga e colonna desiderata.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["filePath"] = new JsonObject { ["type"] = "string", ["description"] = "Percorso assoluto o relativo del file da aprire." },
                        ["line"] = new JsonObject { ["type"] = "integer", ["description"] = "Numero di riga (1-based) a cui posizionare il cursore (opzionale)." },
                        ["column"] = new JsonObject { ["type"] = "integer", ["description"] = "Numero di colonna (1-based) a cui posizionare il cursore (opzionale)." }
                    },
                    ["required"] = new JsonArray { "filePath" }
                }
            },
            new JsonObject
            {
                ["name"] = "norea_compare_files",
                ["description"] = "Apre lo strumento visivo Side-by-Side Diff di NotepadREA per consentire all'utente di confrontare due versioni di un file o revisionare modifiche.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["fileA"] = new JsonObject { ["type"] = "string", ["description"] = "Primo file (sinistra - versione base/originale)." },
                        ["fileB"] = new JsonObject { ["type"] = "string", ["description"] = "Secondo file (destra - nuova versione modificata)." }
                    },
                    ["required"] = new JsonArray { "fileA", "fileB" }
                }
            },
            new JsonObject
            {
                ["name"] = "norea_inspect_hex",
                ["description"] = "Apre l'ispezione esadecimale virtualizzata con calcolo dell'entropia di Shannon e decodifica ASCII in NotepadREA.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["filePath"] = new JsonObject { ["type"] = "string", ["description"] = "Percorso del file binario o testo da ispezionare in esadecimale." }
                    },
                    ["required"] = new JsonArray { "filePath" }
                }
            },
            new JsonObject
            {
                ["name"] = "norea_notify",
                ["description"] = "Mostra un flyout toast scuro moderno sul desktop dell'utente in stile ArtCraft per segnalare aggiornamenti, build riuscite o fine task.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["appName"] = new JsonObject { ["type"] = "string", ["description"] = "Nome dell'app o del task (es. 'NotepadREA', 'Build')." },
                        ["message"] = new JsonObject { ["type"] = "string", ["description"] = "Messaggio di stato dell'operazione." },
                        ["oldVersion"] = new JsonObject { ["type"] = "string", ["description"] = "Versione precedente (opzionale)." },
                        ["newVersion"] = new JsonObject { ["type"] = "string", ["description"] = "Nuova versione (opzionale)." }
                    },
                    ["required"] = new JsonArray { "appName", "message" }
                }
            },
            new JsonObject
            {
                ["name"] = "norea_list_apps",
                ["description"] = "Restituisce l'elenco delle applicazioni NOREA disponibili, il loro stato di installazione, versione e processi in esecuzione.",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["runningOnly"] = new JsonObject { ["type"] = "boolean", ["description"] = "Se true restituisce solo le app attualmente in esecuzione." }
                    }
                }
            },
            new JsonObject
            {
                ["name"] = "norea_launch_app",
                ["description"] = "Avvia un'applicazione della suite NOREA specificandone l'ID (es. 'NotepadREA', 'NoreaLauncher').",
                ["inputSchema"] = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = new JsonObject
                    {
                        ["appId"] = new JsonObject { ["type"] = "string", ["description"] = "Identificativo dell'app da avviare (es. 'NotepadREA')." }
                    },
                    ["required"] = new JsonArray { "appId" }
                }
            }
        };

        var result = new JsonObject
        {
            ["tools"] = tools
        };
        return MakeSuccessResponse(id, result);
    }

    private static JsonObject HandleToolsCall(JsonObject? prms, JsonNode? id)
    {
        string name = prms?["name"]?.GetValue<string>() ?? "";
        var args = prms?["arguments"]?.AsObject() ?? [];

        try
        {
            string outputText = name switch
            {
                "norea_open_file" => ExecuteOpenFile(args),
                "norea_compare_files" => ExecuteCompareFiles(args),
                "norea_inspect_hex" => ExecuteInspectHex(args),
                "norea_notify" => ExecuteNotify(args),
                "norea_list_apps" => ExecuteListApps(args),
                "norea_launch_app" => ExecuteLaunchApp(args),
                _ => throw new InvalidOperationException($"Strumento sconosciuto: {name}")
            };

            var contentArr = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = outputText
                }
            };

            var result = new JsonObject
            {
                ["content"] = contentArr,
                ["isError"] = false
            };
            return MakeSuccessResponse(id, result);
        }
        catch (Exception ex)
        {
            var contentArr = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = $"Errore: {ex.Message}"
                }
            };
            var result = new JsonObject
            {
                ["content"] = contentArr,
                ["isError"] = true
            };
            return MakeSuccessResponse(id, result);
        }
    }

    private static string ExecuteOpenFile(JsonObject args)
    {
        string path = args["filePath"]?.GetValue<string>() ?? throw new ArgumentException("filePath mancante");
        int line = args["line"]?.GetValue<int>() ?? -1;
        int col = args["column"]?.GetValue<int>() ?? -1;

        bool ok = NoreaBridge.OpenInNotepad(path, line, col);
        return ok ? $"File aperto con successo in NotepadREA: {path}" : $"Impossibile aprire il file {path}";
    }

    private static string ExecuteCompareFiles(JsonObject args)
    {
        string a = args["fileA"]?.GetValue<string>() ?? throw new ArgumentException("fileA mancante");
        string b = args["fileB"]?.GetValue<string>() ?? throw new ArgumentException("fileB mancante");

        bool ok = NoreaBridge.DiffInNotepad(a, b);
        return ok ? $"Diff Side-by-Side aperto in NotepadREA tra {a} e {b}" : "Impossibile aprire il Diff Side-by-Side";
    }

    private static string ExecuteInspectHex(JsonObject args)
    {
        string path = args["filePath"]?.GetValue<string>() ?? throw new ArgumentException("filePath mancante");
        bool ok = NoreaBridge.HexInNotepad(path);
        return ok ? $"Ispezione esadecimale aperta in NotepadREA per: {path}" : $"Impossibile aprire la vista esadecimale per {path}";
    }

    private static string ExecuteNotify(JsonObject args)
    {
        string app = args["appName"]?.GetValue<string>() ?? "NotepadREA";
        string msg = args["message"]?.GetValue<string>() ?? "Operazione completata";
        string oldVer = args["oldVersion"]?.GetValue<string>() ?? "v2.1.0";
        string newVer = args["newVersion"]?.GetValue<string>() ?? "v2.2.0";

        bool ok = NoreaBridge.NotifyLauncher(app, msg, oldVer, newVer);
        return ok ? $"Notifica inviata con successo al desktop: [{app}] {msg}" : "Impossibile mostrare la notifica desktop";
    }

    private static string ExecuteListApps(JsonObject args)
    {
        bool runningOnly = args["runningOnly"]?.GetValue<bool>() ?? false;
        var apps = NoreaBridge.GetAppsStatus(runningOnly);
        return JsonSerializer.Serialize(apps, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string ExecuteLaunchApp(JsonObject args)
    {
        string appId = args["appId"]?.GetValue<string>() ?? throw new ArgumentException("appId mancante");
        bool ok = NoreaBridge.LaunchApp(appId);
        return ok ? $"Applicazione {appId} avviata con successo" : $"Impossibile trovare o avviare {appId}";
    }

    private static JsonObject MakeSuccessResponse(JsonNode? id, JsonObject result)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id != null ? JsonNode.Parse(id.ToJsonString()) : null,
            ["result"] = result
        };
    }

    private static JsonObject MakeErrorResponse(JsonNode? id, int code, string message)
    {
        return new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id != null ? JsonNode.Parse(id.ToJsonString()) : null,
            ["error"] = new JsonObject
            {
                ["code"] = code,
                ["message"] = message
            }
        };
    }
}
