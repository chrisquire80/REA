using NotepadREA;
using System.Runtime.ExceptionServices;

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

    [Fact]
    public void Document_SaveTo_RequiresExplicitOverrideForExternalChanges()
    {
        string root = Path.Combine(Path.GetTempPath(), $"norea-save-conflict-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "sample.txt");
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var document = new Document();
                document.Editor.Text = "editor";
                document.SaveTo(path, Theme.Light, "Consolas", 10);
                DateTime originalWriteTime = File.GetLastWriteTimeUtc(path);

                File.WriteAllText(path, "extern");
                File.SetLastWriteTimeUtc(path, originalWriteTime);

                Assert.Throws<ExternalFileChangedException>(
                    () => document.SaveTo(path, Theme.Light, "Consolas", 10));
                Assert.Equal("extern", File.ReadAllText(path));

                document.SaveTo(path, Theme.Light, "Consolas", 10, overwriteExternalChanges: true);
                Assert.Equal("editor", File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        try
        {
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
