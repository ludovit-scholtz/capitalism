using MasterApi.Configuration;
using MasterApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;

namespace MasterApi.Tests;

public sealed class ForwardedHeadersConfigurationTests
{
    [Fact]
    public void TryBuild_WithTrustedProxyAndHopCount_EnablesForwardedHeaders()
    {
        var enabled = ForwardedHeadersConfiguration.TryBuild(
            new ReverseProxyOptions
            {
                ForwardedForHopCount = 1,
                TrustedProxies = ["127.0.0.1", "10.0.0.0/8"]
            },
            out var options);

        Assert.True(enabled);
        Assert.Equal(1, options.ForwardLimit);
        Assert.Contains(options.KnownProxies, proxy => proxy.ToString() == "127.0.0.1");
        Assert.Contains(options.KnownIPNetworks, network => network.PrefixLength == 8);
    }

    [Fact]
    public void TryBuild_WithoutTrustedProxy_DisablesForwardedHeaders()
    {
        var enabled = ForwardedHeadersConfiguration.TryBuild(
            new ReverseProxyOptions
            {
                ForwardedForHopCount = 1,
                TrustedProxies = []
            },
            out _);

        Assert.False(enabled);
    }

    [Fact]
    public void TryBuild_WithInvalidTrustedProxy_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ForwardedHeadersConfiguration.TryBuild(
                new ReverseProxyOptions
                {
                    ForwardedForHopCount = 1,
                    TrustedProxies = ["not-an-ip"]
                },
                out _));
    }

    [Fact]
    public async Task ForwardedHeadersMiddleware_WithTrustedProxy_UsesForwardedClientIp()
    {
        var enabled = ForwardedHeadersConfiguration.TryBuild(
            new ReverseProxyOptions
            {
                ForwardedForHopCount = 1,
                TrustedProxies = ["10.0.0.0/8"]
            },
            out var options);

        Assert.True(enabled);

        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("10.0.0.10");
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.42";

        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Options.Create(options));

        await middleware.Invoke(context);

        Assert.Equal("203.0.113.42", context.Connection.RemoteIpAddress?.ToString());
    }
}
