using System.Globalization;
using System.Text;

namespace MasterApi.Utilities;

/// <summary>
/// Minimal, dependency-free PDF writer that renders a <see cref="LegalDocument"/> into a valid,
/// multi-page A4 PDF using the standard Helvetica fonts. A custom encoding with a Differences array
/// is emitted so Slovak/German diacritics used in the legal text are rendered by mainstream viewers.
/// </summary>
public static class LegalPdfGenerator
{
    private const double PageWidth = 595.0;
    private const double PageHeight = 842.0;
    private const double Margin = 56.0;
    private const double UsableWidth = PageWidth - (2 * Margin);

    private const double TitleSize = 16.0;
    private const double HeadingSize = 12.0;
    private const double BodySize = 10.0;
    private const double MetaSize = 9.0;

    // Maps a Unicode character that is not plain ASCII to a (byte code, PostScript glyph name).
    // Byte codes start at 128 and are emitted via the font /Encoding /Differences array.
    private static readonly (char Char, string Glyph)[] ExtraGlyphs =
    [
        ('\u00A0', "space"), ('\u00E1', "aacute"), ('\u00C1', "Aacute"),
        ('\u010D', "ccaron"), ('\u010C', "Ccaron"), ('\u010F', "dcaron"), ('\u010E', "Dcaron"),
        ('\u00E9', "eacute"), ('\u00C9', "Eacute"), ('\u011B', "ecaron"), ('\u011A', "Ecaron"),
        ('\u00ED', "iacute"), ('\u00CD', "Iacute"), ('\u013A', "lacute"), ('\u0139', "Lacute"),
        ('\u013E', "lcaron"), ('\u013D', "Lcaron"), ('\u0148', "ncaron"), ('\u0147', "Ncaron"),
        ('\u00F3', "oacute"), ('\u00D3', "Oacute"), ('\u00F4', "ocircumflex"), ('\u00D4', "Ocircumflex"),
        ('\u0155', "racute"), ('\u0154', "Racute"), ('\u0159', "rcaron"), ('\u0158', "Rcaron"),
        ('\u0161', "scaron"), ('\u0160', "Scaron"), ('\u0165', "tcaron"), ('\u0164', "Tcaron"),
        ('\u00FA', "uacute"), ('\u00DA', "Uacute"), ('\u016F', "uring"), ('\u016E', "Uring"),
        ('\u00FD', "yacute"), ('\u00DD', "Yacute"), ('\u017E', "zcaron"), ('\u017D', "Zcaron"),
        ('\u00E4', "adieresis"), ('\u00C4', "Adieresis"), ('\u00F6', "odieresis"), ('\u00D6', "Odieresis"),
        ('\u00FC', "udieresis"), ('\u00DC', "Udieresis"), ('\u00DF', "germandbls"),
        ('\u201E', "quotedblbase"), ('\u201C', "quotedblleft"), ('\u201D', "quotedblright"),
        ('\u2018', "quoteleft"), ('\u2019', "quoteright"), ('\u2013', "endash"), ('\u2014', "emdash"),
        ('\u2026', "ellipsis"), ('\u20AC', "Euro"),
    ];

    private static readonly Dictionary<char, byte> GlyphCodes = BuildGlyphCodes();

    private sealed record TextLine(string Text, bool Bold, double Size, double Height);

    public static byte[] Generate(LegalDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var lines = BuildLines(document);
        var pages = Paginate(lines);
        return Render(pages);
    }

    private static Dictionary<char, byte> BuildGlyphCodes()
    {
        var map = new Dictionary<char, byte>();
        byte code = 128;
        foreach (var (ch, _) in ExtraGlyphs)
        {
            map[ch] = code;
            code++;
        }

        return map;
    }

    private static List<TextLine> BuildLines(LegalDocument document)
    {
        var lines = new List<TextLine>();
        AddWrapped(lines, document.Title, bold: true, TitleSize, TitleSize + 6);
        lines.Add(new TextLine($"{document.Title}  |  v{document.Version}  |  {document.EffectiveDate}", false, MetaSize, MetaSize + 6));
        lines.Add(Spacer(6));
        AddWrapped(lines, document.Intro, bold: false, BodySize, BodySize + 4);
        lines.Add(Spacer(8));

        foreach (var section in document.Sections)
        {
            AddWrapped(lines, section.Heading, bold: true, HeadingSize, HeadingSize + 5);
            foreach (var paragraph in section.Paragraphs)
            {
                AddWrapped(lines, paragraph, bold: false, BodySize, BodySize + 4);
                lines.Add(Spacer(3));
            }

            lines.Add(Spacer(6));
        }

        return lines;
    }

    private static TextLine Spacer(double height) => new(string.Empty, false, BodySize, height);

    private static void AddWrapped(List<TextLine> lines, string text, bool bold, double size, double height)
    {
        foreach (var wrapped in WrapText(text, size, bold))
        {
            lines.Add(new TextLine(wrapped, bold, size, height));
        }
    }

    private static IEnumerable<string> WrapText(string text, double size, bool bold)
    {
        var maxChars = Math.Max(8, (int)(UsableWidth / (size * (bold ? 0.56 : 0.52))));
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = new StringBuilder();
        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
            }
            else if (current.Length + 1 + word.Length <= maxChars)
            {
                current.Append(' ').Append(word);
            }
            else
            {
                yield return current.ToString();
                current.Clear();
                current.Append(word);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static List<List<TextLine>> Paginate(List<TextLine> lines)
    {
        var pages = new List<List<TextLine>>();
        var page = new List<TextLine>();
        var y = PageHeight - Margin;
        foreach (var line in lines)
        {
            if (y - line.Height < Margin && page.Count > 0)
            {
                pages.Add(page);
                page = [];
                y = PageHeight - Margin;
            }

            page.Add(line);
            y -= line.Height;
        }

        if (page.Count > 0)
        {
            pages.Add(page);
        }

        if (pages.Count == 0)
        {
            pages.Add([]);
        }

        return pages;
    }

    private static byte[] Render(List<List<TextLine>> pages)
    {
        // Object layout: 1 catalog, 2 pages, 3 encoding, 4 font regular, 5 font bold,
        // then for each page: a page object followed by a content stream object.
        var totalObjects = 5 + (pages.Count * 2);
        var objects = new string[totalObjects + 1];

        var pageObjectNumbers = new List<int>();
        for (var i = 0; i < pages.Count; i++)
        {
            pageObjectNumbers.Add(6 + (i * 2));
        }

        var kids = string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"));
        objects[1] = "<< /Type /Catalog /Pages 2 0 R >>";
        objects[2] = $"<< /Type /Pages /Count {pages.Count} /Kids [{kids}] >>";
        objects[3] = BuildEncodingObject();
        objects[4] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding 3 0 R >>";
        objects[5] = "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding 3 0 R >>";

        for (var i = 0; i < pages.Count; i++)
        {
            var pageObjectNumber = pageObjectNumbers[i];
            var contentObjectNumber = pageObjectNumber + 1;
            objects[pageObjectNumber] =
                "<< /Type /Page /Parent 2 0 R " +
                $"/MediaBox [0 0 {Num(PageWidth)} {Num(PageHeight)}] " +
                "/Resources << /Font << /F1 4 0 R /F2 5 0 R >> >> " +
                $"/Contents {contentObjectNumber} 0 R >>";

            var content = BuildContentStream(pages[i]);
            objects[contentObjectNumber] =
                $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream";
        }

        return Assemble(objects, totalObjects);
    }

    private static string BuildEncodingObject()
    {
        var differences = new StringBuilder();
        differences.Append("128");
        foreach (var (_, glyph) in ExtraGlyphs)
        {
            differences.Append(" /").Append(glyph);
        }

        return $"<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [{differences}] >>";
    }

    private static string BuildContentStream(List<TextLine> lines)
    {
        var builder = new StringBuilder();
        var y = PageHeight - Margin;
        foreach (var line in lines)
        {
            if (!string.IsNullOrEmpty(line.Text))
            {
                var font = line.Bold ? "/F2" : "/F1";
                builder.Append("BT ")
                    .Append(font).Append(' ').Append(Num(line.Size)).Append(" Tf ")
                    .Append("1 0 0 1 ").Append(Num(Margin)).Append(' ').Append(Num(y - line.Size)).Append(" Tm ")
                    .Append('(').Append(EncodeText(line.Text)).Append(") Tj ")
                    .Append("ET\n");
            }

            y -= line.Height;
        }

        return builder.ToString();
    }

    private static string EncodeText(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text)
        {
            if (ch is '(' or ')' or '\\')
            {
                builder.Append('\\').Append(ch);
            }
            else if (ch >= ' ' && ch <= '~')
            {
                builder.Append(ch);
            }
            else if (GlyphCodes.TryGetValue(ch, out var code))
            {
                builder.Append('\\').Append(Convert.ToString(code, 8).PadLeft(3, '0'));
            }
            else
            {
                builder.Append('?');
            }
        }

        return builder.ToString();
    }

    private static byte[] Assemble(string[] objects, int totalObjects)
    {
        var output = new MemoryStream();
        var writer = new StreamWriter(output, new UTF8Encoding(false)) { NewLine = "\n" };
        writer.Write("%PDF-1.4\n");
        writer.Write("%\u00E2\u00E3\u00CF\u00D3\n");
        writer.Flush();

        var offsets = new long[totalObjects + 1];
        for (var i = 1; i <= totalObjects; i++)
        {
            writer.Flush();
            offsets[i] = output.Length;
            writer.Write($"{i} 0 obj\n{objects[i]}\nendobj\n");
        }

        writer.Flush();
        var xrefOffset = output.Length;
        writer.Write($"xref\n0 {totalObjects + 1}\n");
        writer.Write("0000000000 65535 f \n");
        for (var i = 1; i <= totalObjects; i++)
        {
            writer.Write($"{offsets[i].ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");
        }

        writer.Write($"trailer\n<< /Size {totalObjects + 1} /Root 1 0 R >>\n");
        writer.Write($"startxref\n{xrefOffset}\n%%EOF\n");
        writer.Flush();
        return output.ToArray();
    }

    private static string Num(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);
}
