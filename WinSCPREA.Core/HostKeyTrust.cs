namespace WinSCPREA;

public enum HostKeyPromptKind
{
    Trusted,
    FirstContact,
    Changed
}

public readonly record struct HostKeyDecision(HostKeyPromptKind Kind, string StoreKey, string? PreviousFingerprint)
{
    public bool IsTrusted => Kind == HostKeyPromptKind.Trusted;
}

/// <summary>
/// Decisione sulla chiave host SFTP, separata dal MessageBox della finestra.
/// </summary>
public static class HostKeyTrust
{
    public static HostKeyDecision Evaluate(IReadOnlyDictionary<string, string> knownHosts, string host, int port, string fingerprint)
    {
        string key = WinScpSettings.HostKeyId(host, port);
        if (knownHosts.TryGetValue(key, out var known) && known == fingerprint)
            return new HostKeyDecision(HostKeyPromptKind.Trusted, key, known);
        if (knownHosts.TryGetValue(key, out known))
            return new HostKeyDecision(HostKeyPromptKind.Changed, key, known);
        return new HostKeyDecision(HostKeyPromptKind.FirstContact, key, null);
    }

    public static string BuildPrompt(string host, string fingerprint, HostKeyDecision decision)
    {
        if (decision.Kind == HostKeyPromptKind.Changed)
        {
            return $"La chiave host di {host} è cambiata.\n\nRegistrata: SHA256:{decision.PreviousFingerprint}\nRicevuta:   SHA256:{fingerprint}\n\nConferma solo se hai verificato la nuova impronta con l'amministratore del server.";
        }

        return $"Il server {host} presenta questa chiave host SHA-256:\n\nSHA256:{fingerprint}\n\nAccetti la chiave e ti fidi di questo server?";
    }

    public static void Remember(IDictionary<string, string> knownHosts, string host, int port, string fingerprint)
    {
        knownHosts[WinScpSettings.HostKeyId(host, port)] = fingerprint;
    }
}
