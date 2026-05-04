using System.Text.Json;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Pure static helpers for parsing GraphQL JSON responses.
/// Extracted from <see cref="GameApiClient"/> to allow unit testing without HTTP.
/// </summary>
public static class GraphQLResponseParser
{
    /// <summary>
    /// Parses the first error in a GraphQL <c>errors</c> array element and extracts
    /// its human-readable message and optional machine-readable error code.
    /// </summary>
    /// <param name="errorsElement">
    /// The JSON element that is the value of <c>data.errors</c> in a GraphQL response.
    /// Must be a non-empty array.
    /// </param>
    /// <returns>
    /// A tuple of (<paramref name="message"/>, <paramref name="code"/>) where
    /// <paramref name="message"/> is the error message (falls back to "Unknown error" if missing)
    /// and <paramref name="code"/> is the value of <c>extensions.code</c>
    /// (empty string if the extension is absent or has no code).
    /// </returns>
    public static (string message, string code) ParseFirstError(JsonElement errorsElement)
    {
        var first = errorsElement[0];

        var message = first.TryGetProperty("message", out var msgEl)
            ? msgEl.GetString() ?? "Unknown error"
            : "Unknown error";

        var code = string.Empty;
        if (first.TryGetProperty("extensions", out var ext) &&
            ext.TryGetProperty("code", out var codeEl))
            code = codeEl.GetString() ?? string.Empty;

        return (message, code);
    }

    /// <summary>
    /// Returns <c>true</c> when the JSON root element contains an <c>errors</c> array.
    /// </summary>
    public static bool HasErrors(JsonElement root) =>
        root.TryGetProperty("errors", out var errors) &&
        errors.ValueKind == JsonValueKind.Array &&
        errors.GetArrayLength() > 0;

    /// <summary>
    /// Returns <c>true</c> when the JSON root element contains a <c>data</c> object.
    /// </summary>
    public static bool HasData(JsonElement root) =>
        root.TryGetProperty("data", out var data) &&
        data.ValueKind != JsonValueKind.Null;
}
