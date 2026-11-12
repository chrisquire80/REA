using System.Text;
using NotepadREA;

namespace NOREA.Tests;

public class TextCodecTests
{
    [Fact]
    public void DetectEol_DetectsCorrectLineEndings()
    {
        Assert.Equal(ScintillaNET.Eol.CrLf, TextCodec.DetectEol("Riga 1\r\nRiga 2\r\n"));
        Assert.Equal(ScintillaNET.Eol.Lf, TextCodec.DetectEol("Riga 1\nRiga 2\n"));
        Assert.Equal(ScintillaNET.Eol.Cr, TextCodec.DetectEol("Riga 1\rRiga 2\r"));
    }

    [Fact]
    public void HasMixedEol_IdentifiesMixedEndings()
    {
        Assert.False(TextCodec.HasMixedEol("Riga 1\r\nRiga 2\r\nRiga 3\r\n"));
        Assert.False(TextCodec.HasMixedEol("Riga 1\nRiga 2\nRiga 3\n"));
        Assert.True(TextCodec.HasMixedEol("Riga 1\r\nRiga 2\nRiga 3\r\n"));
        Assert.True(TextCodec.HasMixedEol("Riga 1\nRiga 2\rRiga 3\n"));
    }

    [Fact]
    public void LooksBinary_DetectsNullBytesCorrectly()
    {
        byte[] textBytes = Encoding.UTF8.GetBytes("Questo è un testo normale senza caratteri null.");
        byte[] binaryBytes = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0x08, 0x00];

        Assert.False(TextCodec.LooksBinary(textBytes));
        Assert.True(TextCodec.LooksBinary(binaryBytes));
    }

    [Fact]
    public void Utf8Bom_Roundtrip_PreservesContent()
    {
        string sample = "Hello NOREA! È un test di codifica con caratteri accentati àèéìòù.";
        byte[] encoded = TextCodec.Encode(sample, FileEncoding.Utf8Bom);

        Assert.Equal(0xEF, encoded[0]);
        Assert.Equal(0xBB, encoded[1]);
        Assert.Equal(0xBF, encoded[2]);

        var (decoded, enc) = TextCodec.Decode(encoded);
        Assert.Equal(FileEncoding.Utf8Bom, enc);
        Assert.Equal(sample, decoded);
    }

    [Fact]
    public void Utf16Le_Roundtrip_PreservesContent()
    {
        string sample = "Unicode UTF-16 test 🚀";
        byte[] encoded = TextCodec.Encode(sample, FileEncoding.Utf16Le);

        Assert.Equal(0xFF, encoded[0]);
        Assert.Equal(0xFE, encoded[1]);

        var (decoded, enc) = TextCodec.Decode(encoded);
        Assert.Equal(FileEncoding.Utf16Le, enc);
        Assert.Equal(sample, decoded);
    }

    [Fact]
    public void Utf8_Roundtrip_PreservesLargePayload()
    {
        string sample = new('x', 16 * 1024 * 1024);
        byte[] encoded = TextCodec.Encode(sample, FileEncoding.Utf8);

        var (decoded, encoding) = TextCodec.Decode(encoded);

        Assert.Equal(FileEncoding.Utf8, encoding);
        Assert.Equal(sample.Length, decoded.Length);
        Assert.Equal(sample, decoded);
    }
}
