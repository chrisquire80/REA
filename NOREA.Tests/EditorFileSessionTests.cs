using System.Text;
using NotepadREA;

namespace NOREA.Tests;

public class EditorFileSessionTests
{
    [Fact]
    public void Decode_Utf8WithoutBom_KeepsTextAndEol()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("riga\r\nriga\r\n");

        var decoded = EditorFileSession.Decode(bytes);

        Assert.False(decoded.Binary);
        Assert.Equal(FileEncoding.Utf8, decoded.Encoding);
        Assert.Equal("riga\r\nriga\r\n", decoded.Text);
        Assert.False(decoded.MixedEol);
        Assert.Equal(ScintillaNET.Eol.CrLf, decoded.Eol);
        Assert.Equal(bytes.Length, decoded.ByteLength);
    }

    [Fact]
    public void Decode_Utf8BomAndMixedEol_ReportsBoth()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, ..Encoding.UTF8.GetBytes("a\r\nb\nc")];

        var decoded = EditorFileSession.Decode(bytes);

        Assert.Equal(FileEncoding.Utf8Bom, decoded.Encoding);
        Assert.True(decoded.MixedEol);
        Assert.Equal("a\r\nb\nc", decoded.Text);
        Assert.False(decoded.Binary);
    }

    [Fact]
    public void Decode_NullByte_IsBinary()
    {
        var decoded = EditorFileSession.Decode("a\0b"u8.ToArray());

        Assert.True(decoded.Binary);
    }

    [Theory]
    [InlineData(FileEncoding.Utf8Bom)]
    [InlineData(FileEncoding.Utf16Le)]
    [InlineData(FileEncoding.Utf16Be)]
    public void Reinterpret_StripsBom(FileEncoding encoding)
    {
        byte[] bytes = TextCodec.Encode("ciao", encoding);

        string text = EditorFileSession.Reinterpret(bytes, encoding);

        Assert.Equal("ciao", text);
    }

    [Theory]
    [InlineData(false, false, true, true, false, false)]
    [InlineData(true, true, false, false, true, false)]
    [InlineData(true, false, false, true, false, true)]
    [InlineData(true, false, true, false, false, true)]
    [InlineData(true, false, true, true, true, true)]
    [InlineData(true, false, true, true, false, false)]
    public void ShouldRejectSave_MatchesTrackedFileRule(
        bool sameTrackedFile,
        bool overwrite,
        bool exists,
        bool hasSnapshot,
        bool changed,
        bool reject)
    {
        Assert.Equal(reject, EditorFileSession.ShouldRejectSave(sameTrackedFile, overwrite, exists, hasSnapshot, changed));
    }
}
