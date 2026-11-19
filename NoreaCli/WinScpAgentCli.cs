using System.Text.Json;
using WinSCPREA;

namespace NoreaCli;

/// <summary>Agent-facing WinSCPREA commands backed by the same SSH.NET SFTP core as the GUI.</summary>
public static class WinScpAgentCli
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static readonly HashSet<string> FlagOptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "--replace", "--yes", "--dry-run"
    };

    public static async Task<int> RunAsync(string[] rawArgs)
    {
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);
        bool json = rawArgs.Any(arg => arg.Equals("--json", StringComparison.OrdinalIgnoreCase));
        string[] args = rawArgs.Where(arg => !arg.Equals("--json", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        try
        {
            string group = args[0].ToLowerInvariant();
            if (group == "site") return await RunSiteAsync(args[1..], json);
            if (group == "status") return RunStatus(json);
            if (group == "remote") return await RunRemoteAsync(args[1..], json);
            if (group == "upload") return await RunUploadAsync(args[1..], json);
            if (group == "download") return await RunDownloadAsync(args[1..], json);
            return Fail(json, "unknown_command", $"Comando winscp sconosciuto: {args[0]}.");
        }
        catch (HostKeyTrustException ex)
        {
            return Fail(json, "host_key_untrusted", ex.Message, new
            {
                host = ex.Host,
                port = ex.Port,
                fingerprint = $"SHA256:{ex.Fingerprint}",
                previouslyTrustedFingerprint = ex.PreviouslyTrusted is null ? null : $"SHA256:{ex.PreviouslyTrusted}",
                retryOption = "--trust-host-key " + ex.Fingerprint
            }, 3);
        }
        catch (Exception ex)
        {
            return Fail(json, "operation_failed", ex.Message, null, 2);
        }
    }

    private static async Task<int> RunSiteAsync(string[] args, bool json)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintSiteUsage();
            return 0;
        }

        string action = args[0].ToLowerInvariant();
        Arguments parsed = ParseOptions(args[1..]);
        if (action == "list")
        {
            ValidateOptions(parsed, [], []);
            var rows = WinScpSettings.Load().Sites
                .OrderBy(site => site.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(site => new
                {
                    site.Name,
                    site.Host,
                    site.Port,
                    site.Username,
                    site.RemoteDirectory,
                    authentication = site.Authentication.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase) ? "private-key" : "password",
                    privateKeyConfigured = !string.IsNullOrWhiteSpace(site.PrivateKeyPath)
                }).ToArray();
            WriteSuccess(json, new { sites = rows, count = rows.Length });
            return 0;
        }

        if (action == "add")
        {
            ValidateOptions(parsed, ["--host", "--username", "--port", "--remote-dir", "--authentication", "--private-key"], ["--replace", "--dry-run"]);
            if (parsed.Positionals.Count != 1) return Fail(json, "usage", "Uso: norea winscp site add <nome> --host HOST --username UTENTE [--port 22] [--authentication password|private-key] [--private-key FILE]");

            string name = RequiredPositional(parsed, 0, "nome sito").Trim();
            string host = RequiredOption(parsed, "--host").Trim();
            string username = RequiredOption(parsed, "--username").Trim();
            int port = ParsePort(parsed.GetOption("--port") ?? "22");
            string remoteDirectory = SftpSessionService.NormalizeRemote(parsed.GetOption("--remote-dir") ?? "/");
            string authentication = (parsed.GetOption("--authentication") ?? "password").Trim().ToLowerInvariant();
            bool useKey = authentication is "private-key" or "key";
            if (authentication is not ("password" or "private-key" or "key"))
                return Fail(json, "invalid_authentication", "--authentication accetta password oppure private-key.");
            string privateKeyPath = "";
            if (useKey)
            {
                string requestedKeyPath = RequiredOption(parsed, "--private-key");
                privateKeyPath = Path.GetFullPath(requestedKeyPath);
                if (!File.Exists(privateKeyPath)) return Fail(json, "key_not_found", $"File chiave SSH non trovato: {privateKeyPath}");
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username))
                return Fail(json, "missing_value", "Nome sito, host e username devono essere valorizzati.");

            var site = new SiteProfile
            {
                Name = name,
                Host = host,
                Port = port,
                Username = username,
                RemoteDirectory = remoteDirectory,
                Authentication = useKey ? "PrivateKey" : "Password",
                PrivateKeyPath = privateKeyPath
            };

            WinScpSettings settings = WinScpSettings.Load();
            int existingIndex = settings.Sites.FindIndex(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0 && !parsed.HasFlag("--replace"))
                return Fail(json, "site_exists", $"Esiste già un sito chiamato '{name}'. Usa --replace per aggiornarlo.");

            if (parsed.HasFlag("--dry-run"))
            {
                WriteSuccess(json, new { dryRun = true, action = existingIndex >= 0 ? "replace-site" : "add-site", site = SiteSummary(site) });
                return 0;
            }

            if (existingIndex >= 0) settings.Sites[existingIndex] = site;
            else settings.Sites.Add(site);
            settings.Save();
            WriteSuccess(json, new { action = existingIndex >= 0 ? "site-replaced" : "site-added", site = SiteSummary(site) });
            return 0;
        }

        if (action == "remove")
        {
            ValidateOptions(parsed, [], ["--yes", "--dry-run"]);
            if (parsed.Positionals.Count != 1) return Fail(json, "usage", "Uso: norea winscp site remove <nome> --yes");
            string name = RequiredPositional(parsed, 0, "nome sito");
            WinScpSettings settings = WinScpSettings.Load();
            SiteProfile? site = settings.Sites.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (site is null) return Fail(json, "site_not_found", $"Sito '{name}' non trovato.");
            if (parsed.HasFlag("--dry-run"))
            {
                WriteSuccess(json, new { dryRun = true, action = "remove-site", site = SiteSummary(site) });
                return 0;
            }
            if (!parsed.HasFlag("--yes")) return Fail(json, "confirmation_required", "Rimozione non eseguita. Ripeti il comando con --yes.");
            settings.Sites.Remove(site);
            settings.Save();
            WriteSuccess(json, new { action = "site-removed", name = site.Name });
            return 0;
        }

        return Fail(json, "unknown_site_command", $"Comando site sconosciuto: {action}.");
    }

    private static int RunStatus(bool json)
    {
        WinScpSettings settings = WinScpSettings.Load();
        WriteSuccess(json, new
        {
            backend = "SSH.NET",
            connectionModel = "one-shot",
            connected = false,
            siteCount = settings.Sites.Count,
            knownHostCount = settings.KnownHosts.Count,
            settingsPath = WinScpSettings.SettingsPath
        });
        return 0;
    }

    private static async Task<int> RunRemoteAsync(string[] args, bool json)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintRemoteUsage();
            return 0;
        }

        string action = args[0].ToLowerInvariant();
        Arguments parsed = ParseOptions(args[1..]);
        string siteName = RequiredOption(parsed, "--site");

        if (action == "list")
        {
            ValidateOptions(parsed, ["--site", "--path", "--trust-host-key", "--password-env", "--key-passphrase-env"], []);
            ValidateNoPositionals(parsed);
            using SftpSessionService session = await ConnectAsync(parsed, siteName);
            string path = SftpSessionService.NormalizeRemote(parsed.GetOption("--path") ?? GetSite(siteName).RemoteDirectory);
            var entries = await session.ListDirectoryAsync(path);
            var rows = entries.Select(entry => new
            {
                entry.Name,
                path = SftpSessionService.NormalizeRemote(entry.FullPath),
                entry.IsDirectory,
                entry.Length,
                entry.Modified
            }).ToArray();
            WriteSuccess(json, new { site = siteName, path, entries = rows, count = rows.Length });
            return 0;
        }

        if (action == "mkdir")
        {
            ValidateOptions(parsed, ["--site", "--path", "--trust-host-key", "--password-env", "--key-passphrase-env"], ["--dry-run"]);
            ValidateNoPositionals(parsed);
            string path = SftpSessionService.NormalizeRemote(RequiredOption(parsed, "--path"));
            if (parsed.HasFlag("--dry-run"))
            {
                WriteSuccess(json, new { dryRun = true, action = "create-directory", site = siteName, path });
                return 0;
            }
            using SftpSessionService session = await ConnectAsync(parsed, siteName);
            await session.CreateDirectoryAsync(path);
            WriteSuccess(json, new { action = "directory-created-or-existed", site = siteName, path });
            return 0;
        }

        if (action == "rename")
        {
            ValidateOptions(parsed, ["--site", "--from", "--to", "--trust-host-key", "--password-env", "--key-passphrase-env"], ["--yes", "--dry-run"]);
            ValidateNoPositionals(parsed);
            string from = SftpSessionService.NormalizeRemote(RequiredOption(parsed, "--from"));
            string to = SftpSessionService.NormalizeRemote(RequiredOption(parsed, "--to"));
            if (from == "/" || to == "/") return Fail(json, "refuse_root_rename", "La radice SFTP / non può essere rinominata.");
            if (parsed.HasFlag("--dry-run"))
            {
                WriteSuccess(json, new { dryRun = true, action = "rename", site = siteName, from, to });
                return 0;
            }
            if (!parsed.HasFlag("--yes")) return Fail(json, "confirmation_required", "Rinomina non eseguita. Ripeti il comando con --yes.");
            using SftpSessionService session = await ConnectAsync(parsed, siteName);
            await session.RenameAsync(from, to);
            WriteSuccess(json, new { action = "renamed", site = siteName, from, to });
            return 0;
        }

        if (action == "delete")
        {
            ValidateOptions(parsed, ["--site", "--path", "--trust-host-key", "--password-env", "--key-passphrase-env"], ["--yes", "--dry-run"]);
            ValidateNoPositionals(parsed);
            string path = SftpSessionService.NormalizeRemote(RequiredOption(parsed, "--path"));
            if (path == "/") return Fail(json, "refuse_root_delete", "La radice SFTP / non può essere eliminata.");
            if (parsed.HasFlag("--dry-run"))
            {
                WriteSuccess(json, new { dryRun = true, action = "delete-remote-item", site = siteName, path });
                return 0;
            }
            if (!parsed.HasFlag("--yes")) return Fail(json, "confirmation_required", "Eliminazione non eseguita. Ripeti il comando con --yes.");
            using SftpSessionService session = await ConnectAsync(parsed, siteName);
            RemoteFileEntry? entry = await FindRemoteEntryAsync(session, path);
            if (entry is null) return Fail(json, "remote_path_not_found", $"Percorso remoto non trovato: {path}");
            await session.DeleteAsync(entry);
            WriteSuccess(json, new { action = "deleted", site = siteName, path, entry.IsDirectory });
            return 0;
        }

        return Fail(json, "unknown_remote_command", $"Comando remote sconosciuto: {action}.");
    }

    private static async Task<int> RunUploadAsync(string[] args, bool json)
    {
        Arguments parsed = ParseOptions(args);
        ValidateOptions(parsed, ["--site", "--remote-dir", "--trust-host-key", "--password-env", "--key-passphrase-env"], ["--dry-run"]);
        string siteName = RequiredOption(parsed, "--site");
        string remoteDirectory = SftpSessionService.NormalizeRemote(parsed.GetOption("--remote-dir") ?? GetSite(siteName).RemoteDirectory);
        if (parsed.Positionals.Count == 0) return Fail(json, "usage", "Uso: norea winscp upload --site NOME [--remote-dir /PERCORSO] FILE... ");
        string[] localPaths = parsed.Positionals.Select(Path.GetFullPath).ToArray();
        foreach (string localPath in localPaths)
        {
            if (!File.Exists(localPath) && !Directory.Exists(localPath)) return Fail(json, "local_path_not_found", $"Percorso locale non trovato: {localPath}");
        }
        if (parsed.HasFlag("--dry-run"))
        {
            WriteSuccess(json, new { dryRun = true, action = "upload", site = siteName, remoteDirectory, localPaths });
            return 0;
        }
        using SftpSessionService session = await ConnectAsync(parsed, siteName);
        await session.UploadAsync(localPaths, remoteDirectory);
        WriteSuccess(json, new { action = "uploaded", site = siteName, remoteDirectory, localPaths, overwrite = false });
        return 0;
    }

    private static async Task<int> RunDownloadAsync(string[] args, bool json)
    {
        Arguments parsed = ParseOptions(args);
        ValidateOptions(parsed, ["--site", "--remote-path", "--local-dir", "--trust-host-key", "--password-env", "--key-passphrase-env"], ["--dry-run"]);
        string siteName = RequiredOption(parsed, "--site");
        ValidateNoPositionals(parsed);
        string remotePath = SftpSessionService.NormalizeRemote(RequiredOption(parsed, "--remote-path"));
        if (remotePath == "/") return Fail(json, "refuse_root_download", "La radice SFTP / non può essere scaricata come singolo elemento.");
        string localDirectory = Path.GetFullPath(RequiredOption(parsed, "--local-dir"));
        if (parsed.HasFlag("--dry-run"))
        {
            WriteSuccess(json, new { dryRun = true, action = "download", site = siteName, remotePath, localDirectory, overwrite = false });
            return 0;
        }
        using SftpSessionService session = await ConnectAsync(parsed, siteName);
        RemoteFileEntry? entry = await FindRemoteEntryAsync(session, remotePath);
        if (entry is null) return Fail(json, "remote_path_not_found", $"Percorso remoto non trovato: {remotePath}");
        await session.DownloadAsync([entry], localDirectory);
        WriteSuccess(json, new { action = "downloaded", site = siteName, remotePath, localDirectory, overwrite = false });
        return 0;
    }

    private static async Task<SftpSessionService> ConnectAsync(Arguments parsed, string siteName)
    {
        WinScpSettings settings = WinScpSettings.Load();
        SiteProfile site = settings.Sites.FirstOrDefault(item => item.Name.Equals(siteName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Sito '{siteName}' non trovato. Usa 'norea winscp site list'.");

        string passwordVariable = parsed.GetOption("--password-env") ?? "WINSCPREA_PASSWORD";
        string keyPassphraseVariable = parsed.GetOption("--key-passphrase-env") ?? "WINSCPREA_KEY_PASSPHRASE";
        string password = Environment.GetEnvironmentVariable(passwordVariable) ?? "";
        string keyPassphrase = Environment.GetEnvironmentVariable(keyPassphraseVariable) ?? "";
        if (site.Authentication.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(site.PrivateKeyPath) || !File.Exists(site.PrivateKeyPath))
                throw new InvalidOperationException($"Chiave privata SSH non trovata per il sito '{site.Name}'.");
        }
        else if (string.IsNullOrEmpty(password))
        {
            throw new InvalidOperationException($"Password non disponibile. Imposta la variabile d'ambiente {passwordVariable}; il valore non viene salvato nel profilo.");
        }

        string key = WinScpSettings.HostKeyId(site.Host, site.Port);
        settings.KnownHosts.TryGetValue(key, out string? previouslyTrusted);
        string requestedFingerprint = NormalizeFingerprint(parsed.GetOption("--trust-host-key"));
        string? seenFingerprint = null;
        bool rejected = false;
        var session = new SftpSessionService();
        try
        {
            await session.ConnectAsync(new ConnectionRequest(site, password, keyPassphrase), (host, fingerprint) =>
            {
                seenFingerprint = fingerprint;
                if (string.Equals(previouslyTrusted, fingerprint, StringComparison.Ordinal)) return true;
                if (!string.IsNullOrEmpty(requestedFingerprint) && string.Equals(requestedFingerprint, fingerprint, StringComparison.Ordinal))
                {
                    settings.KnownHosts[key] = fingerprint;
                    settings.Save();
                    return true;
                }
                rejected = true;
                return false;
            });
            return session;
        }
        catch
        {
            session.Dispose();
            if (rejected && seenFingerprint is not null)
                throw new HostKeyTrustException(site.Host, site.Port, seenFingerprint, previouslyTrusted);
            throw;
        }
    }

    private static async Task<RemoteFileEntry?> FindRemoteEntryAsync(SftpSessionService session, string path)
    {
        string normalized = SftpSessionService.NormalizeRemote(path);
        if (normalized == "/") return new RemoteFileEntry("/", "/", true, 0, DateTime.MinValue);
        string parent = SftpSessionService.ParentRemote(normalized);
        var entries = await session.ListDirectoryAsync(parent);
        return entries.FirstOrDefault(item => SftpSessionService.NormalizeRemote(item.FullPath).Equals(normalized, StringComparison.Ordinal));
    }

    private static SiteProfile GetSite(string name) =>
        WinScpSettings.Load().Sites.FirstOrDefault(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException($"Sito '{name}' non trovato. Usa 'norea winscp site list'.");

    private static object SiteSummary(SiteProfile site) => new
    {
        site.Name,
        site.Host,
        site.Port,
        site.Username,
        site.RemoteDirectory,
        authentication = site.Authentication.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase) ? "private-key" : "password",
        privateKeyConfigured = !string.IsNullOrWhiteSpace(site.PrivateKeyPath)
    };

    private static int ParsePort(string value)
    {
        if (!int.TryParse(value, out int port) || port is < 1 or > 65535)
            throw new ArgumentException("La porta deve essere un numero tra 1 e 65535.");
        return port;
    }

    private static string NormalizeFingerprint(string? fingerprint)
    {
        string value = fingerprint?.Trim() ?? "";
        return value.StartsWith("SHA256:", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : value;
    }

    private static Arguments ParseOptions(string[] tokens)
    {
        var parsed = new Arguments();
        bool positionalOnly = false;
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!positionalOnly && token == "--") { positionalOnly = true; continue; }
            if (positionalOnly || !token.StartsWith("--", StringComparison.Ordinal))
            {
                parsed.Positionals.Add(token);
                continue;
            }

            int equals = token.IndexOf('=');
            string option = equals > 0 ? token[..equals] : token;
            if (FlagOptions.Contains(option))
            {
                if (equals > 0) throw new ArgumentException($"L'opzione {option} non accetta un valore.");
                if (!parsed.Flags.Add(option)) throw new ArgumentException($"Opzione ripetuta: {option}.");
                continue;
            }

            string value;
            if (equals > 0) value = token[(equals + 1)..];
            else if (i + 1 < tokens.Length && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal)) value = tokens[++i];
            else throw new ArgumentException($"Manca il valore per {option}.");

            if (!parsed.Options.TryAdd(option, value)) throw new ArgumentException($"Opzione ripetuta: {option}.");
        }
        return parsed;
    }

    private static void ValidateOptions(Arguments parsed, string[] allowedOptions, string[] allowedFlags)
    {
        var allowedOptionSet = new HashSet<string>(allowedOptions, StringComparer.OrdinalIgnoreCase);
        var allowedFlagSet = new HashSet<string>(allowedFlags, StringComparer.OrdinalIgnoreCase);
        string? invalid = parsed.Options.Keys.FirstOrDefault(key => !allowedOptionSet.Contains(key))
            ?? parsed.Flags.FirstOrDefault(key => !allowedFlagSet.Contains(key));
        if (invalid is not null) throw new ArgumentException($"Opzione non valida: {invalid}.");
    }

    private static void ValidateNoPositionals(Arguments parsed)
    {
        if (parsed.Positionals.Count > 0) throw new ArgumentException($"Argomento inatteso: {parsed.Positionals[0]}.");
    }

    private static string RequiredOption(Arguments parsed, string name) =>
        !string.IsNullOrWhiteSpace(parsed.GetOption(name)) ? parsed.GetOption(name)!.Trim() : throw new ArgumentException($"Opzione richiesta: {name}.");

    private static string RequiredPositional(Arguments parsed, int index, string description) =>
        index < parsed.Positionals.Count && !string.IsNullOrWhiteSpace(parsed.Positionals[index])
            ? parsed.Positionals[index]
            : throw new ArgumentException($"Valore richiesto: {description}.");

    private static void WriteSuccess(bool json, object data)
    {
        if (json) Console.WriteLine(JsonSerializer.Serialize(new { ok = true, data }, JsonOptions));
        else Console.WriteLine(JsonSerializer.Serialize(data, JsonOptions));
    }

    private static int Fail(bool json, string code, string message, object? details = null, int exitCode = 1)
    {
        if (json) Console.WriteLine(JsonSerializer.Serialize(new { ok = false, error = new { code, message, details } }, JsonOptions));
        else Console.Error.WriteLine($"Errore [{code}]: {message}");
        return exitCode;
    }

    private static void PrintUsage() => Console.WriteLine("""
        Uso: norea winscp <comando> [opzioni] [--json]

        site list
        site add <nome> --host HOST --username UTENTE [--port 22] [--authentication password|private-key] [--private-key FILE] [--replace] [--dry-run]
        site remove <nome> [--yes] [--dry-run]
        status
        remote list --site NOME [--path /PERCORSO] [--trust-host-key SHA256:IMPRONTA]
        remote mkdir --site NOME --path /PERCORSO [--dry-run]
        remote rename --site NOME --from /VECCHIO --to /NUOVO [--yes] [--dry-run]
        remote delete --site NOME --path /PERCORSO [--yes] [--dry-run]
        upload --site NOME [--remote-dir /PERCORSO] FILE...
        download --site NOME --remote-path /PERCORSO --local-dir CARTELLA

        Password e passphrase si leggono solo da variabili d'ambiente (default: WINSCPREA_PASSWORD e WINSCPREA_KEY_PASSPHRASE).
        Nessun comando sovrascrive file esistenti. Rename e delete richiedono --yes; una nuova chiave host richiede --trust-host-key con l'impronta esatta.
        """);

    private static void PrintSiteUsage() => Console.WriteLine("Uso: norea winscp site list | add <nome> ... | remove <nome> [--yes]");
    private static void PrintRemoteUsage() => Console.WriteLine("Uso: norea winscp remote list|mkdir|rename|delete --site NOME ...");

    private sealed class Arguments
    {
        public List<string> Positionals { get; } = [];
        public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Flags { get; } = new(StringComparer.OrdinalIgnoreCase);
        public string? GetOption(string name) => Options.GetValueOrDefault(name);
        public bool HasFlag(string name) => Flags.Contains(name);
    }

    private sealed class HostKeyTrustException(string host, int port, string fingerprint, string? previouslyTrusted)
        : Exception(previouslyTrusted is null
            ? $"Chiave host non ancora verificata per {host}:{port}. Verifica l'impronta SHA256:{fingerprint} e ripeti con --trust-host-key {fingerprint}."
            : $"La chiave host è cambiata per {host}:{port}. Registrata: SHA256:{previouslyTrusted}; ricevuta: SHA256:{fingerprint}. Verifica e ripeti con --trust-host-key {fingerprint}.")
    {
        public string Host { get; } = host;
        public int Port { get; } = port;
        public string Fingerprint { get; } = fingerprint;
        public string? PreviouslyTrusted { get; } = previouslyTrusted;
    }
}
