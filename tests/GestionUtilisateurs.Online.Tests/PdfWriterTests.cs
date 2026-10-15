using GestionUtilisateurs.Shared.Services;

namespace GestionUtilisateurs.Blazor.Tests;

public class PdfWriterTests
{
    /// <summary>Construit un JPEG 1x1 (niveaux de gris) minimaliste mais valide, sans caméra.</summary>
    private static byte[] TinyJpeg()
    {
        var buf = new List<byte>();
        void Add(params byte[] b) => buf.AddRange(b);
        Add(0xFF, 0xD8); // SOI
        Add(0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00); // APP0 JFIF
        Add(0xFF, 0xDB, 0x00, 0x43, 0x00); // DQT luminance
        Add(16, 11, 10, 16, 24, 40, 51, 61, 12, 12, 14, 19, 26, 58, 60, 55, 14, 13, 16, 24, 40, 57, 69, 56, 14, 17, 22, 29, 51, 87, 80, 62, 18, 22, 37, 56, 68, 109, 103, 77, 24, 35, 55, 64, 81, 104, 113, 92, 49, 64, 78, 87, 103, 121, 120, 101, 72, 92, 95, 98, 112, 100, 103, 99);
        Add(0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01, 0x00, 0x01, 0x01, 0x01, 0x11, 0x00); // SOF0 1x1 gris
        Add(0xFF, 0xC4, 0x00, 0x1F, 0x00); // DHT DC luminance
        Add(0x00, 0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00);
        Add(0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B);
        Add(0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00); // SOS
        Add(0x2B); // données compressées (DC=0, EOB)
        Add(0xFF, 0xD9); // EOI
        return buf.ToArray();
    }

    [Fact]
    public void IsJpeg_Detecte()
    {
        Assert.True(PdfWriter.IsJpeg(TinyJpeg()));
        Assert.False(PdfWriter.IsJpeg(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.False(PdfWriter.IsJpeg(null));
    }

    [Fact]
    public void TryGetJpegSize_LitDimensions()
    {
        Assert.True(PdfWriter.TryGetJpegSize(TinyJpeg(), out var w, out var h, out _));
        Assert.Equal(1, w);
        Assert.Equal(1, h);
    }

    [Fact]
    public void FromJpegs_ProduitPdfValide()
    {
        var pdf = PdfWriter.FromJpegs(new[] { TinyJpeg() });

        Assert.NotEmpty(pdf);
        var head = System.Text.Encoding.ASCII.GetString(pdf, 0, 5);
        Assert.Equal("%PDF-", head);
        var text = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.Contains("/Type /Page", text);
        Assert.Contains("%%EOF", text);
    }

    [Fact]
    public void FromJpegs_PlusieursPages()
    {
        var pdf = PdfWriter.FromJpegs(new[] { TinyJpeg(), TinyJpeg(), TinyJpeg() });

        var text = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.Contains("/Count 3", text);
    }

    [Fact]
    public void FromJpegs_SansImage_Echec()
    {
        Assert.Throws<ArgumentException>(() => PdfWriter.FromJpegs(Array.Empty<byte[]>()));
    }

    [Fact]
    public void FromJpegs_NonJpeg_Echec()
    {
        Assert.Throws<InvalidOperationException>(() => PdfWriter.FromJpegs(new[] { new byte[] { 1, 2, 3, 4 } }));
    }
}
