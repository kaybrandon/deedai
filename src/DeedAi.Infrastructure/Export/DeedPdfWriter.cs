using System.Text;

namespace DeedAi.Infrastructure.Export;

/// <summary>
/// QuestPDF-equivalent reviewed-deed / report PDF writer. Emits a valid PDF 1.4
/// without native Skia dependencies so Layout A Windows and Linux tests stay portable.
/// </summary>
public static class DeedPdfWriter
{
    public static byte[] ReviewedDeed(
        string name,
        string client,
        string status,
        string? reviewStatus,
        string? deedType,
        string? assignee,
        DateTimeOffset updatedAt,
        IReadOnlyList<(string Label, string? Value)> fields,
        IReadOnlyList<string> flags)
    {
        var lines = new List<string>
        {
            "Deed AI — Reviewed deed",
            "",
            $"Name: {name}",
            $"Client: {client}",
            $"Status: {status}",
            $"Review status: {reviewStatus ?? "—"}",
            $"Deed type: {deedType ?? "—"}",
            $"Assignee: {assignee ?? "Unassigned"}",
            $"Updated: {updatedAt:u}",
            ""
        };
        lines.AddRange(fields.Select(f => $"{f.Label}: {Display(f.Value)}"));
        lines.Add("");
        lines.Add("Flags: " + (flags.Count == 0 ? "—" : string.Join(", ", flags)));
        lines.Add("");
        lines.Add($"Exported {DateTimeOffset.UtcNow:u} UTC");
        return Render("Deed AI reviewed deed", lines);
    }

    public static byte[] DocumentsReport(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string?>> rows)
    {
        var lines = new List<string>
        {
            "Deed AI — Documents report",
            "",
            $"Rows: {rows.Count}",
            $"Exported {DateTimeOffset.UtcNow:u} UTC",
            "",
            string.Join(" | ", headers)
        };
        foreach (var row in rows)
        {
            lines.Add(string.Join(" | ", row.Select(Display)));
            lines.Add("");
        }

        return Render("Deed AI documents report", lines);
    }

    public static string DashboardFileName(DateTimeOffset from, DateTimeOffset to) =>
        $"deedai-dashboard-{from.UtcDateTime:yyyy-MM-dd}-to-{to.UtcDateTime:yyyy-MM-dd}.pdf";

    public static byte[] Dashboard(
        string title,
        string clientFilter,
        DateTimeOffset rangeFrom,
        DateTimeOffset rangeTo,
        DateTimeOffset generatedAt,
        int uploaded,
        int queued,
        int processing,
        int ready,
        int failed,
        IReadOnlyList<(string Label, int Count)> statusMix,
        IReadOnlyList<string> userLabels,
        IReadOnlyList<(string Label, IReadOnlyList<int> Data)> byUserSeries,
        IReadOnlyList<string> volumeLabels,
        IReadOnlyList<(string Label, IReadOnlyList<int> Data)> volumeSeries)
    {
        var lines = new List<string>
        {
            title,
            "Dashboard",
            $"Range: {rangeFrom.UtcDateTime:yyyy-MM-dd} to {rangeTo.UtcDateTime:yyyy-MM-dd}",
            $"Client: {clientFilter}",
            $"Generated {generatedAt.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC",
            "",
            "Counts",
            $"Uploaded: {uploaded}",
            $"Queued: {queued}",
            $"Processing: {processing}",
            $"Ready: {ready}",
            $"Failed: {failed}",
            "",
            "Status mix"
        };

        if (statusMix.Count == 0 || uploaded == 0)
        {
            lines.Add("No status mix for this range.");
        }
        else
        {
            foreach (var slice in statusMix)
            {
                var pct = uploaded == 0 ? 0 : (int)Math.Round(slice.Count * 100d / uploaded);
                lines.Add($"{slice.Label}: {slice.Count} ({pct}%) {Bar(slice.Count, uploaded)}");
            }
        }

        lines.Add("");
        lines.Add("By user");
        if (userLabels.Count == 0)
        {
            lines.Add("No by-user activity for this range.");
        }
        else
        {
            var userHeaders = new List<string> { "User" };
            userHeaders.AddRange(byUserSeries.Select(s => s.Label));
            lines.Add(string.Join(" | ", userHeaders));
            for (var i = 0; i < userLabels.Count; i++)
            {
                var cells = new List<string> { userLabels[i] };
                cells.AddRange(byUserSeries.Select(s => i < s.Data.Count ? s.Data[i].ToString() : "0"));
                lines.Add(string.Join(" | ", cells));
            }
        }

        lines.Add("");
        lines.Add("Volume over time");
        if (volumeLabels.Count == 0)
        {
            lines.Add("No volume for this range.");
        }
        else
        {
            var volumeHeaders = new List<string> { "Day" };
            volumeHeaders.AddRange(volumeSeries.Select(s => s.Label));
            lines.Add(string.Join(" | ", volumeHeaders));
            for (var i = 0; i < volumeLabels.Count; i++)
            {
                var cells = new List<string> { volumeLabels[i] };
                cells.AddRange(volumeSeries.Select(s => i < s.Data.Count ? s.Data[i].ToString() : "0"));
                lines.Add(string.Join(" | ", cells));
            }
        }

        return Render(title, lines);
    }

    private static string Bar(int value, int max)
    {
        if (max <= 0)
        {
            return "[----------]";
        }

        var filled = Math.Clamp((int)Math.Round(value * 10d / max), 0, 10);
        return "[" + new string('#', filled) + new string('-', 10 - filled) + "]";
    }

    private static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : value.Replace('\r', ' ').Replace('\n', ' ');

    private static byte[] Render(string title, IReadOnlyList<string> lines)
    {
        const int width = 612;
        const int height = 792;
        const int left = 48;
        const int top = 744;
        const int bottom = 48;
        const int leading = 14;

        var pages = new List<string>();
        var y = top;
        var builder = new StringBuilder();
        builder.Append("BT /F1 16 Tf ").Append(left).Append(' ').Append(y).Append(" Td (").Append(PdfEscape(title)).Append(") Tj ET\n");
        y -= 24;
        builder.Append("BT /F1 10 Tf ").Append(left).Append(' ').Append(y).Append(" Td\n");

        foreach (var raw in lines)
        {
            foreach (var wrap in Wrap(raw, 92))
            {
                if (y < bottom)
                {
                    builder.Append("ET\n");
                    pages.Add(builder.ToString());
                    builder.Clear();
                    y = top;
                    builder.Append("BT /F1 10 Tf ").Append(left).Append(' ').Append(y).Append(" Td\n");
                }

                builder.Append("0 -").Append(leading).Append(" Td (").Append(PdfEscape(wrap)).Append(") Tj\n");
                y -= leading;
            }
        }

        builder.Append("ET\n");
        pages.Add(builder.ToString());

        for (var i = 0; i < pages.Count; i++)
        {
            pages[i] += "BT /F1 9 Tf 48 32 Td ("
                + PdfEscape($"Page {i + 1} of {pages.Count}")
                + ") Tj ET\n";
        }

        return Assemble(pages, width, height);
    }

    private static IEnumerable<string> Wrap(string text, int max)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield return " ";
            yield break;
        }

        var remaining = text;
        while (remaining.Length > max)
        {
            var cut = remaining.LastIndexOf(' ', max);
            if (cut < 20)
            {
                cut = max;
            }

            yield return remaining[..cut];
            remaining = remaining[cut..].TrimStart();
        }

        yield return remaining;
    }

    private static string PdfEscape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (c is '(' or ')' or '\\')
            {
                builder.Append('\\').Append(c);
            }
            else if (c is >= (char)32 and <= (char)126)
            {
                builder.Append(c);
            }
            else if (c is '\t')
            {
                builder.Append(' ');
            }
            else
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }

    private static byte[] Assemble(IReadOnlyList<string> pageStreams, int width, int height)
    {
        var objects = new List<byte[]>();
        objects.Add([]); // 1-based

        var catalogNum = 1;
        var pagesNum = 2;
        var fontNum = 3;
        var firstPageNum = 4;

        var pageNums = Enumerable.Range(0, pageStreams.Count).Select(i => firstPageNum + i * 2).ToList();
        var kids = string.Join(" ", pageNums.Select(n => $"{n} 0 R"));

        objects.Add(Obj(catalogNum, $"<< /Type /Catalog /Pages {pagesNum} 0 R >>"));
        objects.Add(Obj(pagesNum, $"<< /Type /Pages /Kids [{kids}] /Count {pageStreams.Count} >>"));
        objects.Add(Obj(fontNum, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"));

        for (var i = 0; i < pageStreams.Count; i++)
        {
            var pageObj = firstPageNum + i * 2;
            var contentObj = pageObj + 1;
            var stream = Encoding.ASCII.GetBytes(pageStreams[i]);
            objects.Add(Obj(pageObj,
                $"<< /Type /Page /Parent {pagesNum} 0 R /MediaBox [0 0 {width} {height}] /Contents {contentObj} 0 R /Resources << /Font << /F1 {fontNum} 0 R >> >> >>"));
            objects.Add(StreamObj(contentObj, stream));
        }

        using var output = new MemoryStream();
        output.Write("%PDF-1.4\n"u8);
        var offsets = new List<long> { 0 };
        for (var i = 1; i < objects.Count; i++)
        {
            offsets.Add(output.Position);
            output.Write(objects[i]);
        }

        var xref = output.Position;
        var count = objects.Count;
        var xrefBuilder = new StringBuilder();
        xrefBuilder.Append("xref\n0 ").Append(count).Append('\n');
        xrefBuilder.Append("0000000000 65535 f \n");
        for (var i = 1; i < count; i++)
        {
            xrefBuilder.Append(offsets[i].ToString("D10")).Append(" 00000 n \n");
        }

        xrefBuilder.Append("trailer\n<< /Size ").Append(count).Append(" /Root 1 0 R >>\nstartxref\n")
            .Append(xref).Append("\n%%EOF\n");
        output.Write(Encoding.ASCII.GetBytes(xrefBuilder.ToString()));
        return output.ToArray();
    }

    private static byte[] Obj(int number, string body) =>
        Encoding.ASCII.GetBytes($"{number} 0 obj\n{body}\nendobj\n");

    private static byte[] StreamObj(int number, byte[] stream)
    {
        var header = Encoding.ASCII.GetBytes($"{number} 0 obj\n<< /Length {stream.Length} >>\nstream\n");
        var footer = "endstream\nendobj\n"u8.ToArray();
        var result = new byte[header.Length + stream.Length + footer.Length];
        Buffer.BlockCopy(header, 0, result, 0, header.Length);
        Buffer.BlockCopy(stream, 0, result, header.Length, stream.Length);
        Buffer.BlockCopy(footer, 0, result, header.Length + stream.Length, footer.Length);
        return result;
    }
}
