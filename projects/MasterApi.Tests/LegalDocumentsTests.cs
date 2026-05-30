using System.Text;
using MasterApi.Utilities;

namespace MasterApi.Tests;

public sealed class LegalDocumentsTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("sk")]
    [InlineData("de")]
    public void TermsContainRequiredProviderAndPaymentInformation(string locale)
    {
        var terms = LegalDocuments.Get(LegalDocumentKind.Terms, locale);
        var text = Flatten(terms);

        Assert.Contains("Scholtz & Company, jsa", text);
        Assert.Contains("51882272", text);
        Assert.Contains("https://asa.gold/terms/latest", text);
        Assert.Contains("ASA.Gold", text);
        Assert.Contains("PayPal", text);
        Assert.Contains("Stripe", text);
        Assert.Contains("Revolut", text);
        Assert.Contains("KYC", text);
    }

    [Theory]
    [InlineData("en", "EU")]
    [InlineData("sk", "EÚ")]
    [InlineData("de", "EU")]
    public void PrivacyPolicyMentionsEuStorageAndProvider(string locale, string euToken)
    {
        var privacy = LegalDocuments.Get(LegalDocumentKind.Privacy, locale);
        var text = Flatten(privacy);

        Assert.Contains("51882272", text);
        Assert.Contains("ASA.Gold", text);
        // Each locale states that data is stored in the European Union.
        Assert.Contains(euToken, text);
    }

    [Theory]
    [InlineData("en", LegalDocumentKind.Terms)]
    [InlineData("sk", LegalDocumentKind.Terms)]
    [InlineData("de", LegalDocumentKind.Privacy)]
    public void GeneratedPdfIsAValidPdfDocument(string locale, LegalDocumentKind kind)
    {
        var document = LegalDocuments.Get(kind, locale);
        var pdf = LegalPdfGenerator.Generate(document);

        Assert.True(pdf.Length > 800);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));
        var content = Encoding.ASCII.GetString(pdf);
        Assert.Contains("/Type /Catalog", content);
        Assert.Contains("startxref", content);
        Assert.EndsWith("%%EOF\n", content);
    }

    [Fact]
    public void AllReturnsTermsAndPrivacyWithDistinctFileNames()
    {
        var documents = LegalDocuments.All("sk");

        Assert.Equal(2, documents.Count);
        Assert.Contains(documents, document => document.Kind == LegalDocumentKind.Terms);
        Assert.Contains(documents, document => document.Kind == LegalDocumentKind.Privacy);

        var termsFile = LegalDocuments.FileName(LegalDocumentKind.Terms, "sk");
        var privacyFile = LegalDocuments.FileName(LegalDocumentKind.Privacy, "sk");
        Assert.NotEqual(termsFile, privacyFile);
        Assert.EndsWith(".pdf", termsFile);
        Assert.EndsWith(".pdf", privacyFile);
    }

    private static string Flatten(LegalDocument document)
    {
        var builder = new StringBuilder();
        builder.AppendLine(document.Title);
        builder.AppendLine(document.Intro);
        foreach (var section in document.Sections)
        {
            builder.AppendLine(section.Heading);
            foreach (var paragraph in section.Paragraphs)
            {
                builder.AppendLine(paragraph);
            }
        }

        return builder.ToString();
    }
}
