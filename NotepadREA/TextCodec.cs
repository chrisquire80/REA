using System.Globalization;
using System.Text;
using ScintillaNET;

namespace NotepadREA;

public enum FileEncoding
{
    Ansi = 0,
    Utf8 = 1,
    Utf8Bom = 2,
    Utf16Le = 3,
    Utf16Be = 4,
    Utf16LeNoBom = 5,
    Utf16BeNoBom = 6,
}

public static class TextCodec
{
    static Encoding? _ansiEncoding;
    static readonly UTF8Encoding Utf8Silent = new(false, false);
    static readonly UTF8Encoding Utf8Strict = new(false, true);

    public static Encoding SystemAnsi
    {
        get
        {
            if (_ansiEncoding != null) return _ansiEncoding;
            try
            {
                int cp = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
                if (cp > 0)
                {
                    _ansiEncoding = Encoding.GetEncoding(cp);
                    return _ansiEncoding;
                }
            }
            catch { }
            _ansiEncoding = Encoding.GetEncoding(1252);
            return _ansiEncoding;
        }
    }

    public static string EncodingLabel(FileEncoding e) => e switch
    {
        FileEncoding.Ansi => $"ANSI ({SystemAnsi.WebName})",
        FileEncoding.Utf8 => "UTF-8",
        FileEncoding.Utf8Bom => "UTF-8-BOM",
        FileEncoding.Utf16Le => "UTF-16 LE BOM",
        FileEncoding.Utf16Be => "UTF-16 BE BOM",
        FileEncoding.Utf16LeNoBom => "UTF-16 LE",
        FileEncoding.Utf16BeNoBom => "UTF-16 BE",
        _ => "Sconosciuta",
    };

    public static bool LooksBinary(byte[] bytes, int sampleSize = 4096)
    {
        if (bytes == null || bytes.Length == 0) return false;
        int checkLen = Math.Min(bytes.Length, sampleSize);

        // Se ha BOM UTF-16 o UTF-8 non è binario
        if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF))) return false;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return false;

        // Se rileva pattern UTF-16 No BOM non è binario
        if (DetectUtf16NoBom(bytes) != null) return false;

        int nulls = 0;
        for (int i = 0; i < checkLen; i++)
        {
            if (bytes[i] == 0) nulls++;
        }
        return nulls > 0;
    }

    public static FileEncoding? DetectUtf16NoBom(byte[] bytes, int sampleSize = 1024)
    {
        if (bytes.Length < 4) return null;
        int checkLen = Math.Min(bytes.Length, sampleSize);
        checkLen -= checkLen % 2;

        int zeroEven = 0;
        int zeroOdd = 0;
        for (int i = 0; i < checkLen; i += 2)
        {
            if (bytes[i] == 0) zeroEven++;
            if (bytes[i + 1] == 0) zeroOdd++;
        }

        int pairs = checkLen / 2;
        if (zeroOdd > pairs * 0.7 && zeroEven < pairs * 0.1) return FileEncoding.Utf16LeNoBom;
        if (zeroEven > pairs * 0.7 && zeroOdd < pairs * 0.1) return FileEncoding.Utf16BeNoBom;
        return null;
    }

    public static (string Text, FileEncoding Encoding) Decode(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return ("", FileEncoding.Utf8);

        // BOM checks
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (Utf8Silent.GetString(bytes, 3, bytes.Length - 3), FileEncoding.Utf8Bom);

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), FileEncoding.Utf16Le);

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), FileEncoding.Utf16Be);

        // UTF-16 No BOM check
        var utf16 = DetectUtf16NoBom(bytes);
        if (utf16 == FileEncoding.Utf16LeNoBom)
            return (Encoding.Unicode.GetString(bytes), FileEncoding.Utf16LeNoBom);
        if (utf16 == FileEncoding.Utf16BeNoBom)
            return (Encoding.BigEndianUnicode.GetString(bytes), FileEncoding.Utf16BeNoBom);

        // Strict UTF-8
        try
        {
            return (Utf8Strict.GetString(bytes), FileEncoding.Utf8);
        }
        catch (DecoderFallbackException)
        {
            return (SystemAnsi.GetString(bytes), FileEncoding.Ansi);
        }
    }

    public static byte[] Encode(string text, FileEncoding enc)
    {
        switch (enc)
        {
            case FileEncoding.Ansi:
                return SystemAnsi.GetBytes(text);
            case FileEncoding.Utf8:
                return Utf8Silent.GetBytes(text);
            case FileEncoding.Utf8Bom:
            {
                var payload = Utf8Silent.GetBytes(text);
                var res = new byte[payload.Length + 3];
                res[0] = 0xEF; res[1] = 0xBB; res[2] = 0xBF;
                Buffer.BlockCopy(payload, 0, res, 3, payload.Length);
                return res;
            }
            case FileEncoding.Utf16Le:
            {
                var payload = Encoding.Unicode.GetBytes(text);
                var res = new byte[payload.Length + 2];
                res[0] = 0xFF; res[1] = 0xFE;
                Buffer.BlockCopy(payload, 0, res, 2, payload.Length);
                return res;
            }
            case FileEncoding.Utf16Be:
            {
                var payload = Encoding.BigEndianUnicode.GetBytes(text);
                var res = new byte[payload.Length + 2];
                res[0] = 0xFE; res[1] = 0xFF;
                Buffer.BlockCopy(payload, 0, res, 2, payload.Length);
                return res;
            }
            case FileEncoding.Utf16LeNoBom:
                return Encoding.Unicode.GetBytes(text);
            case FileEncoding.Utf16BeNoBom:
                return Encoding.BigEndianUnicode.GetBytes(text);
            default:
                return Utf8Silent.GetBytes(text);
        }
    }

    public static Eol DetectEol(string text)
    {
        int i = text.IndexOfAny(['\r', '\n']);
        if (i < 0) return Eol.CrLf;
        if (text[i] == '\n') return Eol.Lf;
        return i + 1 < text.Length && text[i + 1] == '\n' ? Eol.CrLf : Eol.Cr;
    }

    public static bool HasMixedEol(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        bool hasCrLf = false;
        bool hasLfOnly = false;
        bool hasCrOnly = false;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    hasCrLf = true;
                    i++;
                }
                else
                {
                    hasCrOnly = true;
                }
            }
            else if (text[i] == '\n')
            {
                hasLfOnly = true;
            }

            int count = (hasCrLf ? 1 : 0) + (hasLfOnly ? 1 : 0) + (hasCrOnly ? 1 : 0);
            if (count > 1) return true;
        }

        return false;
    }
}
