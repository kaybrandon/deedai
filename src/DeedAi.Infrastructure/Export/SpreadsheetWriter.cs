using System.IO.Compression;
using System.Net;
using System.Text;

namespace DeedAi.Infrastructure.Export;

public static class SpreadsheetWriter
{
    public static byte[] ToCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public static byte[] ToXlsx(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes);
            Write(zip, "_rels/.rels", Rels);
            Write(zip, "xl/workbook.xml", Workbook(sheetName));
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels);
            Write(zip, "xl/worksheets/sheet1.xml", Sheet(headers, rows));
        }

        return stream.ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        var text = value ?? "";
        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Sheet(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var builder = new StringBuilder();
        builder.Append("""<?xml version="1.0" encoding="UTF-8"?>""");
        builder.Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>""");
        builder.Append(RowXml(1, headers));
        var index = 2;
        foreach (var row in rows)
        {
            builder.Append(RowXml(index, row));
            index++;
        }

        builder.Append("</sheetData></worksheet>");
        return builder.ToString();
    }

    private static string RowXml(int index, IReadOnlyList<string?> values)
    {
        var builder = new StringBuilder();
        builder.Append($"<row r=\"{index}\">");
        for (var i = 0; i < values.Count; i++)
        {
            var cell = CellName(i, index);
            var text = WebUtility.HtmlEncode(values[i] ?? "");
            builder.Append($"<c r=\"{cell}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{text}</t></is></c>");
        }

        builder.Append("</row>");
        return builder.ToString();
    }

    private static string CellName(int column, int row)
    {
        var name = "";
        var n = column;
        do
        {
            name = (char)('A' + (n % 26)) + name;
            n = n / 26 - 1;
        } while (n >= 0);

        return name + row;
    }

    private static string Workbook(string sheetName) =>
        $"""<?xml version="1.0" encoding="UTF-8"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{WebUtility.HtmlEncode(sheetName)}" sheetId="1" r:id="rId1"/></sheets></workbook>""";

    private const string ContentTypes =
        """<?xml version="1.0" encoding="UTF-8"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/></Types>""";

    private const string Rels =
        """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""";

    private const string WorkbookRels =
        """<?xml version="1.0" encoding="UTF-8"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/></Relationships>""";
}
