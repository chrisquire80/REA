using NotepadREA;
using Xunit;

namespace NOREA.Tests;

public class TextTransformAndDiffTests
{
    [Fact]
    public void FormatJson_ValidJson_FormatsAndMinifiesCorrectly()
    {
        string raw = "{\"name\":\"Norea\",\"version\":2,\"tools\":[\"NotepadREA\",\"Launcher\"]}";

        string formatted = TextTransformTools.FormatJson(raw, minify: false);
        Assert.Contains("\n", formatted);
        Assert.Contains("  \"name\": \"Norea\"", formatted);

        string minified = TextTransformTools.FormatJson(formatted, minify: true);
        Assert.Equal(raw, minified);
    }

    [Fact]
    public void FormatXml_ValidXml_FormatsAndMinifiesCorrectly()
    {
        string raw = "<root><app id=\"1\"><name>NotepadREA</name></app></root>";

        string formatted = TextTransformTools.FormatXml(raw, minify: false);
        Assert.Contains("\n", formatted);
        Assert.Contains("  <app id=\"1\">", formatted);

        string minified = TextTransformTools.FormatXml(formatted, minify: true);
        Assert.Equal(raw, minified);
    }

    [Fact]
    public void Base64_Roundtrip_MatchesOriginal()
    {
        string text = "NOREA Suite Clean-Room Engineering 2026";
        string encoded = TextTransformTools.Base64Encode(text);
        string decoded = TextTransformTools.Base64Decode(encoded);

        Assert.NotEqual(text, encoded);
        Assert.Equal(text, decoded);
    }

    [Fact]
    public void UrlEncode_Roundtrip_MatchesOriginal()
    {
        string text = "test path/file name?param=value&flag=true";
        string encoded = TextTransformTools.UrlEncode(text);
        string decoded = TextTransformTools.UrlDecode(encoded);

        Assert.Contains("%2F", encoded);
        Assert.Equal(text, decoded);
    }

    [Fact]
    public void SortLines_AscendingAndDescending_SortsCorrectly()
    {
        string input = "Zebra\nApple\nMango\nBanana";

        string asc = TextTransformTools.SortLines(input, descending: false);
        Assert.Equal("Apple\nBanana\nMango\nZebra", asc);

        string desc = TextTransformTools.SortLines(input, descending: true);
        Assert.Equal("Zebra\nMango\nBanana\nApple", desc);
    }

    [Fact]
    public void RemoveDuplicateLines_PreservesFirstOccurrences()
    {
        string input = "Alpha\nBeta\nAlpha\nGamma\nBeta";
        string deduped = TextTransformTools.RemoveDuplicateLines(input);

        Assert.Equal("Alpha\nBeta\nGamma", deduped);
    }

    [Fact]
    public void TrimTrailingSpaces_RemovesSpacesAndTabsAtLineEnds()
    {
        string input = "Line 1   \nLine 2\t\t\nLine 3";
        string trimmed = TextTransformTools.TrimTrailingSpaces(input);

        Assert.Equal("Line 1\nLine 2\nLine 3", trimmed);
    }

    [Fact]
    public void DiffEngine_DetectsAdditionsDeletionsAndModifications()
    {
        string left = "line1\nline2\nline3\nline4";
        string right = "line1\nline2_modified\nline3\nline4\nline5_added";

        var diff = DiffEngine.ComputeDiff(left, right);

        Assert.True(diff.Count >= 5);
        Assert.Equal(DiffLineType.Unchanged, diff[0].Type); // line1
        Assert.Equal(DiffLineType.Modified, diff[1].Type);  // line2 vs line2_modified
        Assert.Equal(DiffLineType.Unchanged, diff[2].Type); // line3
        Assert.Equal(DiffLineType.Unchanged, diff[3].Type); // line4
        Assert.Equal(DiffLineType.Inserted, diff[4].Type);  // line5_added
    }
}
