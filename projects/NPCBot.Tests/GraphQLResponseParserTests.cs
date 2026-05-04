using System.Text.Json;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="GraphQLResponseParser"/> — the pure static helpers
/// that extract error messages, codes, and data presence from GraphQL JSON responses
/// without requiring an HTTP client.
/// </summary>
public sealed class GraphQLResponseParserTests
{
    // ── ParseFirstError ───────────────────────────────────────────────────────

    [Fact]
    public void ParseFirstError_WithCodeAndMessage_ReturnsBoth()
    {
        const string json = """
            [{"message":"Not authenticated.","extensions":{"code":"UNAUTHENTICATED"}}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Not authenticated.", message);
        Assert.Equal("UNAUTHENTICATED", code);
    }

    [Fact]
    public void ParseFirstError_WithDuplicateEmailCode_ReturnsCode()
    {
        const string json = """
            [{"message":"Email already registered.","extensions":{"code":"DUPLICATE_EMAIL"}}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("DUPLICATE_EMAIL", code);
        Assert.Equal("Email already registered.", message);
    }

    [Fact]
    public void ParseFirstError_WithoutExtensions_ReturnsEmptyCode()
    {
        // Some GraphQL errors carry only message (no extensions block)
        const string json = """
            [{"message":"Something went wrong."}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Something went wrong.", message);
        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public void ParseFirstError_WithExtensionsButNoCode_ReturnsEmptyCode()
    {
        // extensions exists but has no "code" key
        const string json = """
            [{"message":"Bad input.","extensions":{"details":"field x is required"}}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Bad input.", message);
        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public void ParseFirstError_WithNullMessage_ReturnsUnknownError()
    {
        // message key present but value is JSON null
        const string json = """
            [{"message":null,"extensions":{"code":"ERR"}}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Unknown error", message);
        Assert.Equal("ERR", code);
    }

    [Fact]
    public void ParseFirstError_WithMissingMessageKey_ReturnsUnknownError()
    {
        // no "message" key at all in the error object
        const string json = """
            [{"extensions":{"code":"NO_MESSAGE"}}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Unknown error", message);
        Assert.Equal("NO_MESSAGE", code);
    }

    [Fact]
    public void ParseFirstError_MultipleErrors_ReturnsFirstOnly()
    {
        // Only the first error in the array should be parsed
        const string json = """
            [
              {"message":"First error.","extensions":{"code":"ERR_ONE"}},
              {"message":"Second error.","extensions":{"code":"ERR_TWO"}}
            ]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("First error.", message);
        Assert.Equal("ERR_ONE", code);
    }

    [Fact]
    public void ParseFirstError_NullCode_ReturnsEmptyCode()
    {
        // extensions.code is JSON null
        const string json = """
            [{"message":"Oops.","extensions":{"code":null}}]
            """;
        using var doc = JsonDocument.Parse(json);
        var (message, code) = GraphQLResponseParser.ParseFirstError(doc.RootElement);

        Assert.Equal("Oops.", message);
        Assert.Equal(string.Empty, code);
    }

    // ── HasErrors ─────────────────────────────────────────────────────────────

    [Fact]
    public void HasErrors_WithErrorsArray_ReturnsTrue()
    {
        const string json = """
            {"errors":[{"message":"Oops."}],"data":null}
            """;
        using var doc = JsonDocument.Parse(json);
        Assert.True(GraphQLResponseParser.HasErrors(doc.RootElement));
    }

    [Fact]
    public void HasErrors_EmptyErrorsArray_ReturnsFalse()
    {
        // An empty errors array means no real errors
        const string json = """{"errors":[],"data":{"me":null}}""";
        using var doc = JsonDocument.Parse(json);
        Assert.False(GraphQLResponseParser.HasErrors(doc.RootElement));
    }

    [Fact]
    public void HasErrors_NoErrorsField_ReturnsFalse()
    {
        const string json = """{"data":{"me":{"id":"1"}}}""";
        using var doc = JsonDocument.Parse(json);
        Assert.False(GraphQLResponseParser.HasErrors(doc.RootElement));
    }

    // ── HasData ───────────────────────────────────────────────────────────────

    [Fact]
    public void HasData_WithDataObject_ReturnsTrue()
    {
        const string json = """{"data":{"me":{"id":"1"}}}""";
        using var doc = JsonDocument.Parse(json);
        Assert.True(GraphQLResponseParser.HasData(doc.RootElement));
    }

    [Fact]
    public void HasData_WithNullData_ReturnsFalse()
    {
        const string json = """{"errors":[{"message":"Auth required."}],"data":null}""";
        using var doc = JsonDocument.Parse(json);
        Assert.False(GraphQLResponseParser.HasData(doc.RootElement));
    }

    [Fact]
    public void HasData_NoDataField_ReturnsFalse()
    {
        const string json = """{"errors":[{"message":"Server error."}]}""";
        using var doc = JsonDocument.Parse(json);
        Assert.False(GraphQLResponseParser.HasData(doc.RootElement));
    }
}
