using Ganss.Xss;

namespace Capitalism.Shared.Security;

public static class AllowlistHtmlSanitizer
{
    private static readonly HtmlSanitizer Sanitizer = BuildSanitizer();

    public static string Sanitize(string html)
    {
        return Sanitizer.Sanitize(html ?? string.Empty);
    }

    private static HtmlSanitizer BuildSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in new[]
                 {
                     "p", "br", "strong", "em", "ul", "ol", "li", "a", "blockquote", "code", "pre",
                     "table", "thead", "tbody", "tr", "th", "td", "div", "span", "hr",
                     "h1", "h2", "h3", "h4", "h5", "h6"
                 })
        {
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in new[] { "href", "title", "class" })
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");
        return sanitizer;
    }
}
