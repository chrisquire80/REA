using System.IO.Pipes;
using System.Text;

namespace NotepadREA;

static class Program
{
    // Per utente: istanze di utenti diversi sulla stessa macchina restano separate.
    static readonly string Id = "NotepadREA." + Environment.UserName;

    [STAThread]
    static void Main(string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // ANSI (cp1252 / system)

        // Supporto salvataggio con privilegi elevati (UAC fallback)
        if (args.Length >= 3 && args[0] == "--elevated-save")
        {
            try
            {
                string tmp = args[1];
                string dest = args[2];
                if (File.Exists(tmp))
                {
                    string? dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.Copy(tmp, dest, overwrite: true);
                    try { File.Delete(tmp); } catch { }
                    return;
                }
                Environment.ExitCode = 1;
                return;
            }
            catch (Exception ex)
            {
                Log.Error("Errore nel salvataggio con privilegi elevati", ex);
                Environment.ExitCode = 1;
                return;
            }
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Errore thread non gestito", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Errore applicativo non gestito", e.ExceptionObject as Exception);

        // Istanza singola (come Notepad++): se l'editor è già aperto gli passa i file e termina.
        using var mutex = new Mutex(true, Id, out bool first);
        if (!first)
        {
            if (SendToRunningInstance(args)) return;
        }

        // Assicura che le DLL native (Scintilla/Lexilla) siano sempre presenti prima dell'istanza UI
        EnsureNativeDependencies();

        ApplicationConfiguration.Initialize();
        var form = new MainForm(args);
        if (first) StartPipeServer(form);
        Application.Run(form);
    }

    static bool SendToRunningInstance(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", Id, PipeDirection.Out);
            client.Connect(2000);
            using var w = new StreamWriter(client, new UTF8Encoding(false));
            w.Write(string.Join("\n", args));
            return true;
        }
        catch { return false; } // istanza bloccata o in chiusura: ne apre una nuova
    }

    static void StartPipeServer(MainForm form)
    {
        var t = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(Id, PipeDirection.In, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                    server.WaitForConnection();
                    using var r = new StreamReader(server, Encoding.UTF8);
                    var args = r.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    while (!form.IsHandleCreated && !form.IsDisposed) Thread.Sleep(20);
                    if (form.IsDisposed) return;
                    form.BeginInvoke(() => form.HandleCommandLineArgs(args));
                }
                catch (ObjectDisposedException) { return; }
                catch (InvalidOperationException) { return; }
                catch (IOException) { /* client caduto: continua ad ascoltare */ }
            }
        }) { IsBackground = true, Name = "NotepadREA pipe" };
        t.Start();
    }

    static void EnsureNativeDependencies()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string targetDir = Path.Combine(baseDir, "runtimes", "win-x64", "native");
            string sciTarget = Path.Combine(targetDir, "Scintilla.dll");
            string lexTarget = Path.Combine(targetDir, "Lexilla.dll");

            if (File.Exists(sciTarget) && File.Exists(lexTarget))
                return;

            Directory.CreateDirectory(targetDir);

            // 1. Prova a copiare da baseDir se presenti a fianco dell'eseguibile
            string sciBeside = Path.Combine(baseDir, "Scintilla.dll");
            string lexBeside = Path.Combine(baseDir, "Lexilla.dll");
            if (!File.Exists(sciTarget) && File.Exists(sciBeside))
                File.Copy(sciBeside, sciTarget, overwrite: true);
            if (!File.Exists(lexTarget) && File.Exists(lexBeside))
                File.Copy(lexBeside, lexTarget, overwrite: true);

            if (File.Exists(sciTarget) && File.Exists(lexTarget))
                return;

            // 2. Estrazione di emergenza dalle risorse incorporate
            var asm = typeof(Program).Assembly;
            ExtractResource(asm, "EmbeddedNative.Scintilla.dll", sciTarget);
            ExtractResource(asm, "EmbeddedNative.Lexilla.dll", lexTarget);
        }
        catch (Exception ex)
        {
            Log.Warn("Impossibile predisporre le librerie native Scintilla", ex);
        }
    }

    static void ExtractResource(System.Reflection.Assembly asm, string resName, string destPath)
    {
        if (File.Exists(destPath)) return;
        using var stream = asm.GetManifestResourceStream(resName);
        if (stream == null) return;
        using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.CopyTo(fs);
    }
}
