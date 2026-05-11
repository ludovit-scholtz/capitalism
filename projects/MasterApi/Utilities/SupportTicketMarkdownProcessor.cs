using System.Text.RegularExpressions;
using Capitalism.Shared.Security;
using Markdig;

namespace MasterApi.Utilities;

public sealed record SupportTicketMarkdownProcessingResult(
    string SanitizedHtml,
    IReadOnlyList<string> ExtractedUrls,
    IReadOnlyList<string> ExtractedImages,
    bool ContainsUnsafeContent,
    string UnsafeReason);

public static class SupportTicketMarkdownProcessor
{
    private static readonly Regex MarkdownLinkRegex = new(
        @"!?\[[^\]]*\]\((?<url>[^)\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex RawUrlRegex = new(
        @"\bhttps?://[^\s<>\""]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ScriptLikeRegex = new(
        @"<\s*(script|iframe|object|embed|svg)|javascript:\s*|data:\s*text/html|on[a-z]+\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    public static SupportTicketMarkdownProcessingResult Process(string markdown)
    {
        var source = markdown ?? string.Empty;
        var extractedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extractedImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in MarkdownLinkRegex.Matches(source))
        {
            var candidate = match.Groups["url"].Value.Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (match.Value.StartsWith("!", StringComparison.Ordinal))
            {
                extractedImages.Add(candidate);
            }
            else
            {
                extractedUrls.Add(candidate);
            }
        }

        foreach (Match match in RawUrlRegex.Matches(source))
        {
            var raw = match.Value.Trim();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                extractedUrls.Add(raw);
            }
        }

        var unsafeReason = string.Empty;
        var containsUnsafe = ScriptLikeRegex.IsMatch(source);

        foreach (var url in extractedUrls.Concat(extractedImages))
        {
            if (!IsSafeUrl(url))
            {
                containsUnsafe = true;
                unsafeReason = "Contains unsupported or unsafe URL scheme.";
                break;
            }
        }

        if (containsUnsafe && string.IsNullOrWhiteSpace(unsafeReason))
        {
            unsafeReason = "Contains potentially unsafe markdown or HTML payload.";
        }

        var html = Markdown.ToHtml(source, Pipeline);
        var sanitized = SanitizeHtml(html);

        return new SupportTicketMarkdownProcessingResult(
            sanitized,
            extractedUrls.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            extractedImages.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            containsUnsafe,
            unsafeReason);
    }

    private static bool IsSafeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static string SanitizeHtml(string html)
    {
        return AllowlistHtmlSanitizer.Sanitize(html);
    }
}
