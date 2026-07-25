using Api.Tests.Infrastructure;

namespace Api.Tests;

public sealed class CatalogImageStaticFileTests
{
    [Fact]
    public async Task GetCatalogImage_ReturnsSvgWithPublicCacheHeaders()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/images/products/iron-ore.svg");

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Headers.CacheControl);
        Assert.True(response.Headers.CacheControl!.Public);
        Assert.Equal(TimeSpan.FromDays(1), response.Headers.CacheControl.MaxAge);
    }

    [Fact]
    public async Task GetCatalogImage_UnknownSlug_ReturnsNotFound()
    {
        using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/images/products/does-not-exist.svg");

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }
}
