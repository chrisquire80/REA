namespace WinSCPREA;

public sealed record SftpConnectionValidation(
    bool Accepted,
    string? ErrorTitle,
    string? ErrorMessage,
    ConnectionRequest? Request);

/// <summary>
/// Controlli della finestra di connessione: host, utente, password o chiave.
/// La password resta solo nella richiesta e non entra nel profilo salvato.
/// </summary>
public static class SftpConnectionFlow
{
    public static SftpConnectionValidation Validate(
        string? name,
        string? host,
        int port,
        string? username,
        string? remoteDirectory,
        bool usePrivateKey,
        string? password,
        string? privateKeyPath,
        string? keyPassphrase)
    {
        string hostText = (host ?? "").Trim();
        string userText = (username ?? "").Trim();
        string keyPath = (privateKeyPath ?? "").Trim();
        string passwordText = password ?? "";

        if (hostText.Length == 0 || userText.Length == 0)
            return new SftpConnectionValidation(false, "Dati mancanti", "Inserisci il server e il nome utente.", null);

        if (usePrivateKey && !File.Exists(keyPath))
            return new SftpConnectionValidation(false, "Chiave non trovata", "Seleziona un file di chiave privata SSH esistente.", null);

        if (!usePrivateKey && passwordText.Length == 0)
            return new SftpConnectionValidation(false, "Password richiesta", "Inserisci la password per continuare.", null);

        string siteName = string.IsNullOrWhiteSpace(name) ? $"{userText}@{hostText}" : name.Trim();
        var site = new SiteProfile
        {
            Name = siteName,
            Host = hostText,
            Port = port,
            Username = userText,
            RemoteDirectory = string.IsNullOrWhiteSpace(remoteDirectory) ? "/" : remoteDirectory.Trim(),
            Authentication = usePrivateKey ? "PrivateKey" : "Password",
            PrivateKeyPath = usePrivateKey ? keyPath : ""
        };

        var request = new ConnectionRequest(
            site,
            usePrivateKey ? "" : passwordText,
            usePrivateKey ? (keyPassphrase ?? "") : "");
        return new SftpConnectionValidation(true, null, null, request);
    }

    public static void UpsertSavedSite(IList<SiteProfile> sites, SiteProfile site)
    {
        var existing = sites.FirstOrDefault(x => x.Name.Equals(site.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            sites.Add(site);
            return;
        }

        existing.Host = site.Host;
        existing.Port = site.Port;
        existing.Username = site.Username;
        existing.RemoteDirectory = site.RemoteDirectory;
        existing.Authentication = site.Authentication;
        existing.PrivateKeyPath = site.PrivateKeyPath;
    }
}
