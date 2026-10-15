using System.IO.Compression;
using System.Text;

namespace GestionUtilisateurs.Shared.Services;

/// <summary>
/// Génère un classeur .xlsx valide (Open XML) sans dépendance externe.
/// Compatible une ou plusieurs feuilles, en-têtes stylés, cellules en chaînes partagées.
/// </summary>
public static class ExcelExporter
{
    private const string AccentHex = "FF4F46E5";

    public sealed record Sheet(string Name, IReadOnlyList<string> Headers, IEnumerable<string[]> Rows);

    public static byte[] Build(string sheetName, IReadOnlyList<string> headers, IEnumerable<string[]> rows)
        => BuildMulti(new[] { new Sheet(sheetName, headers, rows) });

    /// <summary>Construit un classeur avec plusieurs feuilles (chaque feuille dénommée).</summary>
    public static byte[] BuildMulti(IReadOnlyList<Sheet> sheets)
    {
        var shared = new List<string>();
        var sharedIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        int Ref(string value)
        {
            if (!sharedIndex.TryGetValue(value, out var idx))
            {
                shared.Add(value);
                idx = shared.Count - 1;
                sharedIndex[value] = idx;
            }
            return idx;
        }

        int HeaderRef(string h) => Ref(h ?? "");

        string Escape(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

        string ColName(int i)
        {
            var name = "";
            i++;
            while (i > 0)
            {
                i--;
                name = (char)('A' + i % 26) + name;
                i /= 26;
            }
            return name;
        }

        // Prépare toutes les lignes de toutes les feuilles pour alimenter sharedStrings.
        var prepared = new List<(string Name, IReadOnlyList<string> Headers, List<List<int>> Cells)>();
        foreach (var sheet in sheets)
        {
            var collected = new List<List<int>>();
            foreach (var row in sheet.Rows)
            {
                var cells = new List<int>();
                for (var c = 0; c < sheet.Headers.Count; c++)
                    cells.Add(c < row.Length ? Ref(row[c] ?? "") : Ref(""));
                collected.Add(cells);
            }
            prepared.Add((sheet.Name, sheet.Headers, collected));
        }

        var sheetXmlList = new List<string>();
        var totalCells = 0;
        for (var s = 0; s < prepared.Count; s++)
        {
            var (name, headers, collected) = prepared[s];
            totalCells += collected.Count * headers.Count;

            var sheetXml = new StringBuilder();
            sheetXml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sheetXml.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
            sheetXml.Append("<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>");
            sheetXml.Append("<cols>");
            for (var c = 0; c < headers.Count; c++)
                sheetXml.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"22\" customWidth=\"1\"/>");
            sheetXml.Append("</cols>");
            sheetXml.Append("<sheetData>");

            // Ligne d'en-tête
            sheetXml.Append("<row r=\"1\">");
            for (var c = 0; c < headers.Count; c++)
                sheetXml.Append($"<c r=\"{ColName(c)}1\" s=\"1\" t=\"s\"><v>{HeaderRef(headers[c])}</v></c>");
            sheetXml.Append("</row>");

            var r = 2;
            foreach (var cells in collected)
            {
                sheetXml.Append($"<row r=\"{r}\">");
                for (var c = 0; c < cells.Count; c++)
                    sheetXml.Append($"<c r=\"{ColName(c)}{r}\" s=\"0\" t=\"s\"><v>{cells[c]}</v></c>");
                sheetXml.Append("</row>");
                r++;
            }

            sheetXml.Append("</sheetData></worksheet>");
            sheetXmlList.Add(sheetXml.ToString());
        }

        var sharedXml = new StringBuilder();
        sharedXml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sharedXml.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" count=\"")
            .Append(totalCells).Append("\" uniqueCount=\"").Append(shared.Count).Append("\">");
        foreach (var s in shared)
        {
            sharedXml.Append("<si><t xml:space=\"preserve\">").Append(Escape(s)).Append("</t></si>");
        }
        sharedXml.Append("</sst>");

        var contentTypes = new StringBuilder();
        contentTypes.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        contentTypes.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        contentTypes.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        contentTypes.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        contentTypes.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        for (var s = 0; s < prepared.Count; s++)
            contentTypes.Append($"<Override PartName=\"/xl/worksheets/sheet{s + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        contentTypes.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        contentTypes.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");
        contentTypes.Append("</Types>");

        var workbook = new StringBuilder();
        workbook.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        workbook.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
        workbook.Append("<sheets>");
        for (var s = 0; s < prepared.Count; s++)
            workbook.Append($"<sheet name=\"{Escape(prepared[s].Name)}\" sheetId=\"{s + 1}\" r:id=\"rId{s + 1}\"/>");
        workbook.Append("</sheets></workbook>");

        var workbookRels = new StringBuilder();
        workbookRels.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        workbookRels.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        for (var s = 0; s < prepared.Count; s++)
            workbookRels.Append($"<Relationship Id=\"rId{s + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{s + 1}.xml\"/>");
        workbookRels.Append($"<Relationship Id=\"rId{prepared.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        workbookRels.Append($"<Relationship Id=\"rId{prepared.Count + 2}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");
        workbookRels.Append("</Relationships>");

        const string styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"2\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><color rgb=\"FFFFFFFF\"/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"3\">" +
            "<fill><patternFill patternType=\"none\"/></fill>" +
            "<fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"" + AccentHex + "\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "</fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"2\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            WriteEntry(zip, "[Content_Types].xml", contentTypes.ToString());
            WriteEntry(zip, "_rels/.rels", rels);
            WriteEntry(zip, "xl/workbook.xml", workbook.ToString());
            WriteEntry(zip, "xl/_rels/workbook.xml.rels", workbookRels.ToString());
            WriteEntry(zip, "xl/styles.xml", styles);
            WriteEntry(zip, "xl/sharedStrings.xml", sharedXml.ToString());
            for (var s = 0; s < prepared.Count; s++)
                WriteEntry(zip, $"xl/worksheets/sheet{s + 1}.xml", sheetXmlList[s]);
        }
        return ms.ToArray();
    }

    private const string rels = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }
}