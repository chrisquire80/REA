using System.IO.Pipes;

namespace NoreaLauncher;

static class Program
{
    static readonly string PipeId = "NoreaLauncher." + Environment.UserName;

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--export-preview")
        {
            try
            {
                ApplicationConfiguration.Initialize();
                using var previewForm = new MainForm();
                previewForm.Show();
                Application.DoEvents();
                previewForm.ExportPreviews(args[1]);
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(args[1], "export_error.txt"), ex.ToString());
            }
            return;
        }

        if (args.Length >= 1 && (args[0] == "--notify" || args[0] == "-notify"))
        {
            string appName = args.Length > 1 ? args[1] : "NotepadREA";
            string status = args.Length > 2 ? args[2] : "Task completato con successo!";
            string oldVer = args.Length > 3 ? args[3] : "v2.1.0";
            string newVer = args.Length > 4 ? args[4] : "v2.2.0";

            ApplicationConfiguration.Initialize();
            var flyout = new UpdateNotificationFlyout(
                appName: appName,
                oldVersion: oldVer,
                newVersion: newVer,
                author: "by NOREA");
            flyout.PositionAtBottomRight();
            flyout.SetCompleted(status);
            Application.Run(flyout);
            return;
        }

        using var mutex = new Mutex(true, PipeId, out bool first);
        if (!first)
        {
            SignalRunningInstance();
            return;
        }

        ApplicationConfiguration.Initialize();
        var form = new MainForm();
        StartPipeServer(form);
        Application.Run(form);
    }

    static void SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeId, PipeDirection.Out);
            client.Connect(1500);
            client.WriteByte(1);
        }
        catch { }
    }

    static void StartPipeServer(MainForm form)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeId, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                    server.WaitForConnection();
                    _ = server.ReadByte();
                    while (!form.IsHandleCreated && !form.IsDisposed) Thread.Sleep(20);
                    if (form.IsDisposed) return;
                    form.BeginInvoke(form.RestoreFromTray);
                }
                catch (ObjectDisposedException) { return; }
                catch (InvalidOperationException) { return; }
                catch (IOException) { }
            }
        }) { IsBackground = true, Name = "NoreaLauncher pipe" };
        t.Start();
    }
}
