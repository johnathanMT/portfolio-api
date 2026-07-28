using System.Text;

namespace PortfolioApi.Services;

/// <summary>
/// Dependency-free minimal PDF generator (valid PDF 1.4, one page, Helvetica).
/// Produces the placeholder Vedin reading document. Swap for QuestPDF later for a
/// full encyclopedia layout. ASCII text only (Helvetica has no Burmese glyphs).
/// </summary>
public static class MiniPdf
{
    public static byte[] Build(string title, IReadOnlyList<string> lines)
    {
        string content = BuildContent(title, lines);
        var objs = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        for (int i = 0; i < objs.Count; i++)
        {
            offsets.Add(sb.Length);               // ASCII → char count == byte offset
            sb.Append($"{i + 1} 0 obj\n{objs[i]}\nendobj\n");
        }
        int xref = sb.Length;
        sb.Append($"xref\n0 {objs.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var off in offsets)
            sb.Append(off.ToString("D10") + " 00000 n \n");
        sb.Append($"trailer\n<< /Size {objs.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static string BuildContent(string title, IReadOnlyList<string> lines)
    {
        var sb = new StringBuilder();
        sb.Append($"BT /F1 22 Tf 72 780 Td ({Esc(title)}) Tj ET\n");
        int y = 738;
        foreach (var line in lines)
        {
            sb.Append($"BT /F1 12 Tf 72 {y} Td ({Esc(line)}) Tj ET\n");
            y -= 20;
            if (y < 60) break;
        }
        return sb.ToString();
    }

    // PDF string escaping + ASCII fold.
    private static string Esc(string s)
    {
        var ascii = new string((s ?? string.Empty).Where(c => c is >= ' ' and < (char)127).ToArray());
        return ascii.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}
