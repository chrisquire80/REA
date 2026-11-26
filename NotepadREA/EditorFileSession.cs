using System.Text;
using ScintillaNET;

namespace NotepadREA;

/// <summary>
/// Apertura, reinterpretazione e conflitto di salvataggio, senza creare un editor.
/// <see cref="Document"/> usa gli stessi metodi.
/// </summary>
public readonly record struct DecodedFile(
    int ByteLength,
    bool Binary,
    string Text,
    FileEncoding Encoding,
    bool MixedEol,
    Eol Eol);

public static class EditorFileSession
{
    public static DecodedFile Decode(byte[] bytes)
    {
        bool binary = TextCodec.LooksBinary(bytes);
        (string text, FileEncoding encoding) = TextCodec.Decode(bytes);
        return new DecodedFile(
            bytes.Length,
            binary,
            text,
            encoding,
            TextCodec.HasMixedEol(text),
            TextCodec.DetectEol(text));
    }

    public static string Reinterpret(byte[] bytes, FileEncoding enc) => enc switch
    {
        FileEncoding.Ansi => TextCodec.SystemAnsi.GetString(bytes),
        FileEncoding.Utf8 => new UTF8Encoding(false).GetString(bytes),
        FileEncoding.Utf8Bom => new UTF8Encoding(false).GetString(bytes.Length >= 3 ? bytes[3..] : bytes),
        FileEncoding.Utf16Le or FileEncoding.Utf16LeNoBom => Encoding.Unicode.GetString(bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE ? bytes[2..] : bytes),
        FileEncoding.Utf16Be or FileEncoding.Utf16BeNoBom => Encoding.BigEndianUnicode.GetString(bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF ? bytes[2..] : bytes),
        _ => new UTF8Encoding(false).GetString(bytes),
    };

    /// <summary>
    /// Stessa condizione di <see cref="Document.SaveTo"/>: un file già tracciato,
    /// senza consenso esplicito, non si sovrascrive se manca, se non c'è snapshot
    /// o se lo snapshot non coincide più col disco.
    /// </summary>
    public static bool ShouldRejectSave(
        bool sameTrackedFile,
        bool overwriteExternalChanges,
        bool fileExists,
        bool hasSnapshot,
        bool snapshotChanged)
    {
        if (!sameTrackedFile || overwriteExternalChanges) return false;
        return !fileExists || !hasSnapshot || snapshotChanged;
    }
}
