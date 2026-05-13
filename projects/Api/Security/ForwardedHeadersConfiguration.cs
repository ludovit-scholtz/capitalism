using System.Net;
using Api.Configuration;
using Microsoft.AspNetCore.HttpOverrides;

namespace Api.Security;

public static class ForwardedHeadersConfiguration
{
    public static bool TryBuild(ReverseProxyOptions reverseProxy, out ForwardedHeadersOptions options)
    {
        options = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor,
        };
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        if (reverseProxy.ForwardedForHopCount <= 0)
        {
            return false;
        }

        foreach (var entry in reverseProxy.TrustedProxies)
        {
            AddTrustedProxy(entry, options);
        }

        if (options.KnownProxies.Count == 0 && options.KnownIPNetworks.Count == 0)
        {
            return false;
        }

        options.ForwardLimit = reverseProxy.ForwardedForHopCount;
        return true;
    }

    private static void AddTrustedProxy(string value, ForwardedHeadersOptions options)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return;
        }

        var slashIndex = trimmed.IndexOf('/');
        if (slashIndex < 0)
        {
            if (!IPAddress.TryParse(trimmed, out var proxyAddress))
            {
                throw new InvalidOperationException(
                    $"Invalid ReverseProxy:TrustedProxies entry '{trimmed}'. Expected IP address or CIDR.");
            }

            options.KnownProxies.Add(proxyAddress);
            return;
        }

        var addressPart = trimmed[..slashIndex].Trim();
        var prefixPart = trimmed[(slashIndex + 1)..].Trim();

        if (!IPAddress.TryParse(addressPart, out var networkAddress))
        {
            throw new InvalidOperationException(
                $"Invalid ReverseProxy:TrustedProxies CIDR '{trimmed}'. Network address is invalid.");
        }

        if (!int.TryParse(prefixPart, out var prefixLength))
        {
            throw new InvalidOperationException(
                $"Invalid ReverseProxy:TrustedProxies CIDR '{trimmed}'. Prefix length is invalid.");
        }

        var maxPrefixLength = networkAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxPrefixLength)
        {
            throw new InvalidOperationException(
                $"Invalid ReverseProxy:TrustedProxies CIDR '{trimmed}'. Prefix must be between 0 and {maxPrefixLength}.");
        }

        options.KnownIPNetworks.Add(new System.Net.IPNetwork(networkAddress, prefixLength));
    }
}
