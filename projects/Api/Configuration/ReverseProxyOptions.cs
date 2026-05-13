namespace Api.Configuration;

/// <summary>
/// Reverse-proxy trust settings for safe forwarded-header processing.
/// </summary>
public sealed class ReverseProxyOptions
{
    public const string SectionName = "ReverseProxy";

    /// <summary>
    /// Maximum trusted X-Forwarded-For hop count. Set to 0 to disable forwarded-header processing.
    /// </summary>
    public int ForwardedForHopCount { get; init; } = 0;

    /// <summary>
    /// Trusted proxy IP addresses or CIDR ranges allowed to supply X-Forwarded-For.
    /// </summary>
    public string[] TrustedProxies { get; init; } = [];
}
