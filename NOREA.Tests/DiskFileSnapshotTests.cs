using NotepadREA;

namespace NOREA.Tests;

public class DiskFileSnapshotTests
{
    [Fact]
    public void HasChanged_DetectsLengthAndTimestampChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-disk-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "sample.txt");
        try
        {
            File.WriteAllText(path, "old");
            var snapshot = DiskFileSnapshot.Capture(path);

            File.WriteAllText(path, "new content");
            File.SetLastWriteTimeUtc(path, snapshot.LastWriteUtc.AddSeconds(3));

            Assert.True(snapshot.HasChanged(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HasChanged_ThrowsWhenFileWasDeleted()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-disk-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "sample.txt");
        try
        {
            File.WriteAllText(path, "content");
            var snapshot = DiskFileSnapshot.Capture(path);
            File.Delete(path);

            Assert.Throws<FileNotFoundException>(() => snapshot.HasChanged(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void HasChanged_DetectsContentChangesWithSameLengthAndTimestamp()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-disk-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "sample.txt");
        try
        {
            File.WriteAllText(path, "old");
            var snapshot = DiskFileSnapshot.Capture(path);

            File.WriteAllText(path, "new");
            File.SetLastWriteTimeUtc(path, snapshot.LastWriteUtc);

            Assert.True(snapshot.HasChanged(path));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
