namespace Api.Security;

public sealed record AuthenticatedSession(string Token, DateTime ExpiresAtUtc);
