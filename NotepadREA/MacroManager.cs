namespace NotepadREA;

public sealed class MacroStep
{
    public int Message { get; set; }
    public IntPtr WParam { get; set; }
    public IntPtr LParam { get; set; }
}

public sealed class MacroManager
{
    public static MacroManager Instance { get; } = new();

    public bool IsRecording { get; private set; }
    public List<MacroStep> Steps { get; } = [];

    public event EventHandler? RecordingStateChanged;

    public void StartRecording(Document? doc)
    {
        if (doc == null || IsRecording) return;
        Steps.Clear();
        IsRecording = true;
        doc.Editor.DirectMessage(3001); // SCI_STARTRECORD
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void StopRecording(Document? doc)
    {
        if (!IsRecording) return;
        IsRecording = false;
        doc?.Editor.DirectMessage(3002); // SCI_STOPRECORD
        RecordingStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RecordStep(int msg, UIntPtr w, IntPtr l)
    {
        if (!IsRecording) return;
        Steps.Add(new MacroStep { Message = msg, WParam = (IntPtr)(long)(ulong)w, LParam = l });
    }

    public void Play(Document? doc, int times = 1)
    {
        if (doc == null || IsRecording || Steps.Count == 0) return;
        for (int i = 0; i < Math.Max(1, times); i++)
        {
            foreach (var step in Steps)
            {
                doc.Editor.DirectMessage(step.Message, step.WParam, step.LParam);
            }
        }
    }
}
