using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using NoreaLauncher;

namespace NOREA.Tests;

public class CloudUpdateServiceTests
{
    [Fact]
    public async Task DownloadAndApplyUpdate_RejectsReleaseWithoutSha256Digest()
    {
        var release = new CloudReleaseInfo(
            true, "v2.0.0", "v1.0.0", "release", "",
            "https://github.com/owner/repo/releases/download/v2/package.zip",
            100, "https://github.com/owner/repo/releases", DateTime.UtcNow);

        var result = await CloudUpdateService.DownloadAndApplyUpdateAsync(release, Path.GetTempPath());

        Assert.False(result.Success);
        Assert.Contains("SHA-256", result.Message);
    }

    [Fact]
    public async Task ApplyUpdateArchive_UpdatesFilesAfterHashValidation()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-update-test-{Guid.NewGuid():N}");
        string target = Path.Combine(root, "install");
        string archivePath = Path.Combine(root, "update.zip");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "app.txt"), "old");
        CreateArchive(archivePath, ("app.txt", "new"), ("nested/config.json", "{}"));
        try
        {
            string hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(archivePath)));
            var result = await CloudUpdateService.ApplyUpdateArchiveAsync(archivePath, target, hash);

            Assert.True(result.Success, result.Message);
            Assert.Equal("new", File.ReadAllText(Path.Combine(target, "app.txt")));
            Assert.Equal("{}", File.ReadAllText(Path.Combine(target, "nested", "config.json")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyUpdateArchive_RejectsHashMismatchWithoutChangingInstall()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-update-test-{Guid.NewGuid():N}");
        string target = Path.Combine(root, "install");
        string archivePath = Path.Combine(root, "update.zip");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "app.txt"), "old");
        CreateArchive(archivePath, ("app.txt", "new"));
        try
        {
            var result = await CloudUpdateService.ApplyUpdateArchiveAsync(archivePath, target, new string('0', 64));

            Assert.False(result.Success);
            Assert.Equal("old", File.ReadAllText(Path.Combine(target, "app.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyUpdateArchive_RejectsPathsOutsideInstall()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-update-test-{Guid.NewGuid():N}");
        string target = Path.Combine(root, "install");
        string archivePath = Path.Combine(root, "update.zip");
        Directory.CreateDirectory(target);
        CreateArchive(archivePath, ("../escape.txt", "outside"));
        try
        {
            var result = await CloudUpdateService.ApplyUpdateArchiveAsync(archivePath, target);

            Assert.False(result.Success);
            Assert.False(File.Exists(Path.Combine(root, "escape.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyUpdateArchive_RollsBackAlreadyAppliedFilesWhenNextFileFails()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-update-test-{Guid.NewGuid():N}");
        string target = Path.Combine(root, "install");
        string archivePath = Path.Combine(root, "update.zip");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "a.txt"), "old");
        Directory.CreateDirectory(Path.Combine(target, "z.txt"));
        CreateArchive(archivePath, ("a.txt", "new"), ("z.txt", "cannot replace a directory"));
        try
        {
            var result = await CloudUpdateService.ApplyUpdateArchiveAsync(archivePath, target);

            Assert.False(result.Success);
            Assert.Equal("old", File.ReadAllText(Path.Combine(target, "a.txt")));
            Assert.True(Directory.Exists(Path.Combine(target, "z.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadAndApplyUpdate_UsesHttpClientAndVerifiesDownloadedAsset()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-update-test-{Guid.NewGuid():N}");
        string target = Path.Combine(root, "install");
        Directory.CreateDirectory(target);
        byte[] zip = CreateArchiveBytes(("app.txt", "downloaded"));
        string hash = Convert.ToHexStringLower(SHA256.HashData(zip));
        using var client = new HttpClient(new ArchiveHandler(zip));
        var release = new CloudReleaseInfo(
            true, "v2.0.0", "v1.0.0", "release", "",
            "https://github.com/owner/repo/releases/download/v2/package.zip", zip.Length,
            "https://github.com/owner/repo/releases", DateTime.UtcNow, hash);
        try
        {
            var result = await CloudUpdateService.DownloadAndApplyUpdateAsync(release, target, httpClient: client);

            Assert.True(result.Success, result.Message);
            Assert.Equal("downloaded", File.ReadAllText(Path.Combine(target, "app.txt")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateArchive(string archivePath, params (string Path, string Content)[] files)
    {
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var (path, content) in files)
        {
            using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    private static byte[] CreateArchiveBytes(params (string Path, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(path).Open(), new UTF8Encoding(false));
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    private sealed class ArchiveHandler(byte[] archive) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(archive),
                RequestMessage = request
            };
            return Task.FromResult(response);
        }
    }
}
