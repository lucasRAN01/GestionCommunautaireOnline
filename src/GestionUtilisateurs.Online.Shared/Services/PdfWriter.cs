using System.Text;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Générateur PDF minimaliste et 100 % hors ligne : assemble une ou plusieurs
/// images JPEG en un document PDF (une page par image, sans dépendance externe).
/// Les images sont embarquées telles quelles (filtre DCTDecode).
/// </summary>
public static class PdfWriter
{
    public static byte[] FromJpegs(IReadOnlyList<byte[]> images)
    {
        if (images == null || images.Count == 0)
            throw new ArgumentException("Aucune image fournie.", nameof(images));

        var pages = new List<(byte[] Data, int W, int H, bool Gray)>();
        foreach (var img in images)
        {
            if (img == null || !TryGetJpegSize(img, out var w, out var h, out var gray))
                throw new InvalidOperationException("Image JPEG illisible : seule la capture photo (JPEG) peut être convertie en PDF.");
            pages.Add((img, w, h, gray));
        }

        // Numéros d'objets : 1 = catalogue, 2 = pages, puis par page : image, contenu, page.
        var pageIds = new List<(int Img, int Content, int Page)>();
        var next = 3;
        foreach (var _ in pages)
        {
            pageIds.Add((next, next + 1, next + 2));
            next += 3;
        }
        var total = next - 1;

        var buf = new List<byte>();
        void Ascii(string s) => buf.AddRange(Encoding.ASCII.GetBytes(s));
        var offsets = new long[total + 1];

        buf.AddRange(new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34, 0x0A, 0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A });

        void BeginObj(int id)
        {
            offsets[id] = buf.Count;
            Ascii(id + " 0 obj\n");
        }
        const string EndObj = "endobj\n";

        BeginObj(1);
        Ascii("<< /Type /Catalog /Pages 2 0 R >>\n" + EndObj);

        BeginObj(2);
        Ascii("<< /Type /Pages /Kids [");
        foreach (var p in pageIds) Ascii(p.Page + " 0 R ");
        Ascii("] /Count " + pages.Count + " >>\n" + EndObj);

        for (var k = 0; k < pages.Count; k++)
        {
            var (data, w, h, gray) = pages[k];
            var ids = pageIds[k];
            var cs = gray ? "DeviceGray" : "DeviceRGB";

            BeginObj(ids.Img);
            Ascii("<< /Type /XObject /Subtype /Image /Width " + w + " /Height " + h
                + " /ColorSpace /" + cs + " /BitsPerComponent 8 /Filter /DCTDecode /Length " + data.Length + " >>\nstream\n");
            buf.AddRange(data);
            Ascii("\nendstream\n" + EndObj);

            var content = "q\n" + w + " 0 0 " + h + " 0 0 cm\n/Im" + k + " Do\nQ\n";
            var contentBytes = Encoding.ASCII.GetBytes(content);
            BeginObj(ids.Content);
            Ascii("<< /Length " + contentBytes.Length + " >>\nstream\n");
            buf.AddRange(contentBytes);
            Ascii("\nendstream\n" + EndObj);

            BeginObj(ids.Page);
            Ascii("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + w + " " + h + "]"
                + " /Resources << /XObject << /Im" + k + " " + ids.Img + " 0 R >> >>"
                + " /Contents " + ids.Content + " 0 R >>\n" + EndObj);
        }

        var xrefPos = buf.Count;
        Ascii("xref\n0 " + (total + 1) + "\n");
        Ascii("0000000000 65535 f \n");
        for (var id = 1; id <= total; id++)
            Ascii(offsets[id].ToString("D10") + " 00000 n \n");
        Ascii("trailer\n<< /Size " + (total + 1) + " /Root 1 0 R >>\nstartxref\n" + xrefPos + "\n%%EOF");

        return buf.ToArray();
    }

    public static bool IsJpeg(byte[]? data) =>
        data != null && data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;

    /// <summary>Lit les dimensions d'une image JPEG (marqueur SOF). Retourne aussi si l'image est en niveaux de gris.</summary>
    public static bool TryGetJpegSize(byte[] data, out int width, out int height, out bool gray)
    {
        width = 0; height = 0; gray = false;
        if (!IsJpeg(data) || data.Length < 20) return false;
        var i = 2;
        while (i + 9 < data.Length)
        {
            if (data[i] != 0xFF) { i++; continue; }
            var marker = data[i + 1];
            while (marker == 0xFF && i + 2 < data.Length) { i++; marker = data[i + 1]; }
            if (marker == 0xD8 || marker == 0xD9 || marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7)) { i += 2; continue; }
            // SOF0-SOF15 sauf DHT (C4), JPG (C8) et DAC (CC).
            if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
            {
                height = (data[i + 5] << 8) | data[i + 6];
                width = (data[i + 7] << 8) | data[i + 8];
                gray = data[i + 9] == 1;
                return width > 0 && height > 0;
            }
            if (i + 3 >= data.Length) return false;
            var len = (data[i + 2] << 8) | data[i + 3];
            if (len < 2) return false;
            i += 2 + len;
        }
        return false;
    }
}
