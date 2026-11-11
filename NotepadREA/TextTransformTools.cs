using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace NotepadREA;

/// <summary>
/// Strumenti di formattazione, codifica e manipolazione testo ad alte prestazioni.
/// </summary>
public static class TextTransformTools
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly JsonSerializerOptions MinifiedJson = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string FormatJson(string input, bool minify = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        using var doc = JsonDocument.Parse(input);
        return JsonSerializer.Serialize(doc.RootElement, minify ? MinifiedJson : IndentedJson);
    }

    public static string FormatXml(string input, bool minify = false)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        var xdoc = XDocument.Parse(input);
        if (minify)
        {
            return xdoc.ToString(SaveOptions.DisableFormatting);
        }
        return xdoc.ToString(SaveOptions.None);
    }

    public static string Base64Encode(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        byte[] bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes);
    }

    public static string Base64Decode(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        byte[] bytes = Convert.FromBase64String(input.Trim());
        return Encoding.UTF8.GetString(bytes);
    }

    public static string UrlEncode(string input)
    {
        return string.IsNullOrEmpty(input) ? input : Uri.EscapeDataString(input);
    }

    public static string UrlDecode(string input)
    {
        return string.IsNullOrEmpty(input) ? input : Uri.UnescapeDataString(input);
    }

    public static string SortLines(string input, bool descending = false)
    {
        if (string.IsNullOrEmpty(input)) return input;
        bool isCrLf = input.Contains("\r\n");
        string eol = isCrLf ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var sorted = descending
            ? lines.OrderByDescending(l => l, StringComparer.OrdinalIgnoreCase)
            : lines.OrderBy(l => l, StringComparer.OrdinalIgnoreCase);

        return string.Join(eol, sorted);
    }

    public static string RemoveDuplicateLines(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        bool isCrLf = input.Contains("\r\n");
        string eol = isCrLf ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>(lines.Length);
        foreach (var l in lines)
        {
            if (seen.Add(l)) result.Add(l);
        }
        return string.Join(eol, result);
    }

    public static string TrimTrailingSpaces(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        bool isCrLf = input.Contains("\r\n");
        string eol = isCrLf ? "\r\n" : "\n";
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd(' ', '\t');
        }
        return string.Join(eol, lines);
    }

    public static string ToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.ToLower());
    }
}
