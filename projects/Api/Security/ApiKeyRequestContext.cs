namespace Api.Security;

public sealed record ApiKeyRequestContext(
    Guid KeyId,
    Guid PlayerId,
    string[] Scopes,
    Guid[] CompanyIds)
{
    public const string HttpContextItemKey = "capitalism/api-key-context";
}
