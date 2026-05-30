using MasterApi.Utilities;

namespace MasterApi.Types;

public sealed class LegalDocumentSectionResult
{
    public string Heading { get; init; } = string.Empty;

    public IReadOnlyList<string> Paragraphs { get; init; } = [];
}

public sealed class LegalDocumentResult
{
    public string Kind { get; init; } = string.Empty;

    public string Locale { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public string EffectiveDate { get; init; } = string.Empty;

    public string Intro { get; init; } = string.Empty;

    public IReadOnlyList<LegalDocumentSectionResult> Sections { get; init; } = [];

    public static LegalDocumentResult From(LegalDocument document) => new()
    {
        Kind = document.Kind == LegalDocumentKind.Terms ? "TERMS" : "PRIVACY",
        Locale = document.Locale,
        Title = document.Title,
        Version = document.Version,
        EffectiveDate = document.EffectiveDate,
        Intro = document.Intro,
        Sections = document.Sections
            .Select(section => new LegalDocumentSectionResult
            {
                Heading = section.Heading,
                Paragraphs = section.Paragraphs,
            })
            .ToList(),
    };
}

public sealed partial class Query
{
    /// <summary>
    /// Returns the publicly available legal documents (Terms &amp; Conditions and Privacy Policy)
    /// in the requested locale. The master frontend renders these and the same content is attached
    /// to the first registration email as PDF.
    /// </summary>
    public IReadOnlyList<LegalDocumentResult> GetLegalDocuments(string? locale) =>
        LegalDocuments.All(locale ?? "en")
            .Select(LegalDocumentResult.From)
            .ToList();
}
