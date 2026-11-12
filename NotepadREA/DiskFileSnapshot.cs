using System.Security.Cryptography;

namespace NotepadREA;

public readonly record struct DiskFileSnapshot(DateTime LastWriteUtc, long Length, string Sha256)
{
    public static DiskFileSnapshot Capture(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists) throw new FileNotFoundException("File non trovato.", path);
        using var stream = info.OpenRead();
        string hash = Convert.ToHexStringLower(SHA256.HashData(stream));
        info.Refresh();
        if (!info.Exists) throw new FileNotFoundException("File non trovato.", path);
        return new DiskFileSnapshot(info.LastWriteTimeUtc, info.Length, hash);
    }

    public static DiskFileSnapshot FromBytes(string path, byte[] bytes)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists) throw new FileNotFoundException("File non trovato.", path);
        return new DiskFileSnapshot(
            info.LastWriteTimeUtc,
            info.Length,
            Convert.ToHexStringLower(SHA256.HashData(bytes)));
    }

    public bool HasChanged(string path)
    {
        var current = Capture(path);
        return current.LastWriteUtc != LastWriteUtc || current.Length != Length ||
               !string.Equals(current.Sha256, Sha256, StringComparison.Ordinal);
    }

    public bool MetadataChanged(string path)
    {
        var info = new FileInfo(path);
        info.Refresh();
        if (!info.Exists) throw new FileNotFoundException("File non trovato.", path);
        return info.LastWriteTimeUtc != LastWriteUtc || info.Length != Length;
    }
}

public sealed class ExternalFileChangedException(string path)
    : IOException($"Il file è stato modificato da un altro programma: {path}");
