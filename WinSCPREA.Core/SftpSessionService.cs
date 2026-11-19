using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace WinSCPREA;

public sealed class SftpSessionService : IDisposable
{
    private SftpClient? _client;
    private PrivateKeyFile? _privateKeyFile;

    public bool IsConnected => _client?.IsConnected == true;
    public string? ServerVersion => _client?.ConnectionInfo.ServerVersion;

    public Task ConnectAsync(ConnectionRequest request, Func<string, string, bool> decideHostKey) => Task.Run(() =>
    {
        Disconnect();
        var site = request.Site;
        AuthenticationMethod authentication;
        if (site.Authentication.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase))
        {
            _privateKeyFile = new PrivateKeyFile(site.PrivateKeyPath, request.KeyPassphrase);
            authentication = new PrivateKeyAuthenticationMethod(site.Username, _privateKeyFile);
        }
        else
        {
            authentication = new PasswordAuthenticationMethod(site.Username, request.Password);
        }

        var connectionInfo = new ConnectionInfo(site.Host, site.Port, site.Username, authentication)
        {
            Timeout = TimeSpan.FromSeconds(25)
        };
        var client = new SftpClient(connectionInfo);
        client.HostKeyReceived += (_, e) => e.CanTrust = decideHostKey(site.Host, e.FingerPrintSHA256);
        try
        {
            client.Connect();
            _client = client;
        }
        catch
        {
            client.Dispose();
            _privateKeyFile?.Dispose();
            _privateKeyFile = null;
            throw;
        }
    });

    public Task<IReadOnlyList<RemoteFileEntry>> ListDirectoryAsync(string path) => Task.Run<IReadOnlyList<RemoteFileEntry>>(() =>
    {
        var client = RequireClient();
        return client.ListDirectory(NormalizeRemote(path))
            .Where(item => item.Name is not "." and not "..")
            .Select(item => new RemoteFileEntry(item.Name, item.FullName, item.Attributes.IsDirectory,
                item.Attributes.Size > long.MaxValue ? long.MaxValue : (long)item.Attributes.Size, item.LastWriteTime))
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    });

    public Task UploadAsync(IEnumerable<string> localPaths, string remoteDirectory, Action<string, ulong>? progress = null) => Task.Run(() =>
    {
        var client = RequireClient();
        string targetDirectory = NormalizeRemote(remoteDirectory);
        EnsureRemoteDirectory(client, targetDirectory);
        foreach (string path in localPaths)
        {
            string fullPath = Path.GetFullPath(path);
            string name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string destination = CombineRemote(targetDirectory, name);
            UploadPath(client, fullPath, destination, progress);
        }
    });

    public Task DownloadAsync(IEnumerable<RemoteFileEntry> entries, string localDirectory, Action<string, ulong>? progress = null) => Task.Run(() =>
    {
        var client = RequireClient();
        string targetDirectory = Path.GetFullPath(localDirectory);
        Directory.CreateDirectory(targetDirectory);
        foreach (var entry in entries)
        {
            string destination = Path.Combine(targetDirectory, entry.Name);
            DownloadPath(client, entry.FullPath, destination, progress);
        }
    });

    public Task CreateDirectoryAsync(string path) => Task.Run(() => EnsureRemoteDirectory(RequireClient(), NormalizeRemote(path)));

    public Task RenameAsync(string oldPath, string newPath) => Task.Run(() =>
    {
        var client = RequireClient();
        string target = NormalizeRemote(newPath);
        if (client.Exists(target)) throw new IOException($"Esiste già un elemento con il nome {target}.");
        client.RenameFile(NormalizeRemote(oldPath), target);
    });

    public Task DeleteAsync(RemoteFileEntry entry) => Task.Run(() => DeletePath(RequireClient(), entry.FullPath, entry.IsDirectory));

    public void Disconnect()
    {
        var client = _client;
        _client = null;
        try
        {
            if (client?.IsConnected == true) client.Disconnect();
        }
        finally
        {
            client?.Dispose();
            _privateKeyFile?.Dispose();
            _privateKeyFile = null;
        }
    }

    public void Dispose() => Disconnect();

    private SftpClient RequireClient() => IsConnected
        ? _client!
        : throw new InvalidOperationException("La session SFTP non è connessa.");

    private static void UploadPath(SftpClient client, string localPath, string remotePath, Action<string, ulong>? progress)
    {
        var attributes = File.GetAttributes(localPath);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"I collegamenti simbolici locali non vengono trasferiti: {localPath}");

        if ((attributes & FileAttributes.Directory) != 0)
        {
            EnsureRemoteDirectory(client, remotePath);
            foreach (string child in Directory.EnumerateFileSystemEntries(localPath))
            {
                string name = Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                UploadPath(client, child, CombineRemote(remotePath, name), progress);
            }
            return;
        }

        if (client.Exists(remotePath))
            throw new IOException($"Il file remoto esiste già e non è stato sovrascritto: {remotePath}");
        using var input = new FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128, FileOptions.SequentialScan);
        client.UploadFile(input, remotePath, false, transferred => progress?.Invoke(Path.GetFileName(localPath), transferred));
    }

    private static void DownloadPath(SftpClient client, string remotePath, string localPath, Action<string, ulong>? progress)
    {
        var attributes = client.GetAttributes(remotePath);
        if (attributes.IsDirectory)
        {
            Directory.CreateDirectory(localPath);
            foreach (var child in client.ListDirectory(remotePath).Where(item => item.Name is not "." and not ".."))
                DownloadPath(client, child.FullName, Path.Combine(localPath, child.Name), progress);
            return;
        }

        if (File.Exists(localPath))
            throw new IOException($"Il file locale esiste già e non è stato sovrascritto: {localPath}");
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        using var output = new FileStream(localPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, FileOptions.SequentialScan);
        client.DownloadFile(remotePath, output, transferred => progress?.Invoke(Path.GetFileName(localPath), transferred));
    }

    private static void EnsureRemoteDirectory(SftpClient client, string path)
    {
        string normalized = NormalizeRemote(path);
        if (normalized == "/") return;
        if (client.Exists(normalized))
        {
            if (!client.GetAttributes(normalized).IsDirectory)
                throw new IOException($"Esiste già un file con il nome {normalized}.");
            return;
        }

        string parent = ParentRemote(normalized);
        if (parent != normalized) EnsureRemoteDirectory(client, parent);
        client.CreateDirectory(normalized);
    }

    private static void DeletePath(SftpClient client, string path, bool isDirectory)
    {
        if (!isDirectory)
        {
            client.DeleteFile(path);
            return;
        }

        foreach (var child in client.ListDirectory(path).Where(item => item.Name is not "." and not ".."))
            DeletePath(client, child.FullName, child.Attributes.IsDirectory);
        client.DeleteDirectory(path);
    }

    public static string NormalizeRemote(string path)
    {
        string normalized = (string.IsNullOrWhiteSpace(path) ? "/" : path.Trim()).Replace('\\', '/');
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        while (normalized.Contains("//", StringComparison.Ordinal)) normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
    }

    public static string CombineRemote(string parent, string child) =>
        NormalizeRemote(parent) == "/" ? "/" + child.Trim('/') : NormalizeRemote(parent) + "/" + child.Trim('/');

    public static string ParentRemote(string path)
    {
        string normalized = NormalizeRemote(path);
        if (normalized == "/") return "/";
        int separator = normalized.LastIndexOf('/');
        return separator <= 0 ? "/" : normalized[..separator];
    }
}
