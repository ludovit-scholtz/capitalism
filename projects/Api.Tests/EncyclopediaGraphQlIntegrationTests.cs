using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class EncyclopediaGraphQlIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public EncyclopediaGraphQlIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
        => ExecuteGraphQlAsync(_client, query, variables, token);

    private async Task<string> RegisterAndGetTokenAsync(string email = "encyclopedia@test.com", string displayName = "Encyclopedia Tester", string password = "TestPass123!")
    {
        var result = await ExecuteGraphQlAsync(
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email, displayName, password } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task EncyclopediaResources_EmptySearch_ReturnsMixedCatalogPage()
    {
        var result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaResources($page: Int!) {
              encyclopediaResources(page: $page) {
                page
                totalPages
                totalCount
                items {
                  slug
                  kind
                  category
                  industry
                  unitSymbol
                  isPerishable
                }
              }
            }
            """,
            new { page = 1 });

        var page = result.GetProperty("data").GetProperty("encyclopediaResources");
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.True(page.GetProperty("totalPages").GetInt32() >= 2);
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 10);

        var items = page.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        Assert.Contains(items.EnumerateArray(), item => item.GetProperty("kind").GetString() == "PRODUCT");
        Assert.Contains(items.EnumerateArray(), item => item.GetProperty("isPerishable").GetBoolean());
    }

    [Fact]
    public async Task EncyclopediaResources_SearchFiltersIronEntries()
    {
        var result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaResources($search: String, $page: Int!) {
              encyclopediaResources(search: $search, page: $page) {
                totalCount
                items {
                  slug
                  name
                  kind
                  category
                  description
                }
              }
            }
            """,
            new { search = "iron", page = 1 });

        var page = result.GetProperty("data").GetProperty("encyclopediaResources");
        var items = page.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);
        Assert.True(page.GetProperty("totalCount").GetInt32() >= items.GetArrayLength());
        Assert.Contains(items.EnumerateArray(), item => item.GetProperty("slug").GetString() == "iron-ore");
        Assert.All(items.EnumerateArray().ToList(), item =>
        {
            var combined =
                $"{item.GetProperty("slug").GetString()} " +
                $"{item.GetProperty("name").GetString()} " +
                $"{item.GetProperty("category").GetString()} " +
                $"{item.GetProperty("description").GetString()}";
            Assert.Contains("iron", combined, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task EncyclopediaResources_Pagination_ReturnsDifferentPages()
    {
        var page1Result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaResources($page: Int!) {
              encyclopediaResources(page: $page) {
                page
                totalPages
                items { slug }
              }
            }
            """,
            new { page = 1 });

        var page2Result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaResources($page: Int!) {
              encyclopediaResources(page: $page) {
                page
                totalPages
                items { slug }
              }
            }
            """,
            new { page = 2 });

        var page1 = page1Result.GetProperty("data").GetProperty("encyclopediaResources");
        var page2 = page2Result.GetProperty("data").GetProperty("encyclopediaResources");

        Assert.Equal(1, page1.GetProperty("page").GetInt32());
        Assert.Equal(2, page2.GetProperty("page").GetInt32());
        Assert.Equal(page1.GetProperty("totalPages").GetInt32(), page2.GetProperty("totalPages").GetInt32());

        var page1Slugs = page1.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("slug").GetString()).ToHashSet();
        var page2Slugs = page2.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("slug").GetString()).ToHashSet();
        Assert.DoesNotContain(page1Slugs, slug => slug is not null && page2Slugs.Contains(slug));
    }

    [Fact]
    public async Task EncyclopediaResourceDetail_ProductSlug_ReturnsProducedByAndUsedInRecipes()
    {
        var result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaDetail($slug: String!) {
              encyclopediaResourceDetail(slug: $slug) {
                entry {
                  slug
                  kind
                  industry
                }
                producedByRecipes {
                  buildingType
                  outputQuantity
                  inputs {
                    slug
                    kind
                    quantity
                  }
                  output {
                    slug
                    kind
                  }
                }
                usedInRecipes {
                  output {
                    slug
                    kind
                  }
                  inputs {
                    slug
                    quantity
                  }
                }
              }
            }
            """,
            new { slug = "flour" });

        var detail = result.GetProperty("data").GetProperty("encyclopediaResourceDetail");
        Assert.Equal("flour", detail.GetProperty("entry").GetProperty("slug").GetString());
        Assert.Equal("PRODUCT", detail.GetProperty("entry").GetProperty("kind").GetString());
        Assert.Equal("FOOD_PROCESSING", detail.GetProperty("entry").GetProperty("industry").GetString());

        var producedBy = detail.GetProperty("producedByRecipes");
        Assert.Single(producedBy.EnumerateArray());
        var flourRecipe = producedBy[0];
        Assert.Equal("FACTORY", flourRecipe.GetProperty("buildingType").GetString());
        Assert.Contains(flourRecipe.GetProperty("inputs").EnumerateArray(), input => input.GetProperty("slug").GetString() == "grain");

        var usedIn = detail.GetProperty("usedInRecipes");
        Assert.True(usedIn.GetArrayLength() > 0);
        Assert.Contains(usedIn.EnumerateArray(), recipe =>
            recipe.GetProperty("output").GetProperty("slug").GetString() != "flour"
            && recipe.GetProperty("inputs").EnumerateArray().Any(input => input.GetProperty("slug").GetString() == "flour"));
    }

    [Fact]
    public async Task EncyclopediaResourceDetail_ResourceSlug_ReturnsMiningAndDownstreamRecipes()
    {
        var result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaDetail($slug: String!) {
              encyclopediaResourceDetail(slug: $slug) {
                entry {
                  slug
                  kind
                  category
                }
                producedByRecipes {
                  buildingType
                  output { slug }
                }
                usedInRecipes {
                  output { slug kind }
                  inputs { slug quantity }
                }
              }
            }
            """,
            new { slug = "wood" });

        var detail = result.GetProperty("data").GetProperty("encyclopediaResourceDetail");
        Assert.Equal("wood", detail.GetProperty("entry").GetProperty("slug").GetString());
        Assert.Equal("RESOURCE", detail.GetProperty("entry").GetProperty("kind").GetString());
        Assert.Equal("ORGANIC", detail.GetProperty("entry").GetProperty("category").GetString());

        var producedBy = detail.GetProperty("producedByRecipes");
        Assert.Single(producedBy.EnumerateArray());
        Assert.Equal("MINE", producedBy[0].GetProperty("buildingType").GetString());

        var usedIn = detail.GetProperty("usedInRecipes");
        Assert.True(usedIn.GetArrayLength() > 0);
        Assert.Contains(usedIn.EnumerateArray(), recipe =>
            recipe.GetProperty("output").GetProperty("kind").GetString() == "PRODUCT"
            && recipe.GetProperty("inputs").EnumerateArray().Any(input => input.GetProperty("slug").GetString() == "wood"));
    }

    [Fact]
    public async Task EncyclopediaResourceDetail_UnknownSlug_ReturnsNull()
    {
        var result = await ExecuteGraphQlAsync(
            """
            query EncyclopediaDetail($slug: String!) {
              encyclopediaResourceDetail(slug: $slug) {
                entry { slug }
              }
            }
            """,
            new { slug = "unknown-encyclopedia-slug" });

        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").GetProperty("encyclopediaResourceDetail").ValueKind);
    }

    [Fact]
    public async Task EncyclopediaResourceDetail_RespectsProSubscriptionUnlockFlags()
    {
        var freeResult = await ExecuteGraphQlAsync(
            """
            query EncyclopediaDetail($slug: String!) {
              encyclopediaResourceDetail(slug: $slug) {
                usedInRecipes {
                  output {
                    slug
                    isProOnly
                    isUnlockedForCurrentPlayer
                  }
                }
              }
            }
            """,
            new { slug = "silicon" });

        foreach (var recipe in freeResult.GetProperty("data").GetProperty("encyclopediaResourceDetail").GetProperty("usedInRecipes").EnumerateArray())
        {
            var output = recipe.GetProperty("output");
            if (output.GetProperty("isProOnly").GetBoolean())
            {
                Assert.False(output.GetProperty("isUnlockedForCurrentPlayer").GetBoolean());
            }
        }

        var token = await RegisterAndGetTokenAsync("encyclopedia-pro-detail@test.com", "Encyclopedia Pro Detail");
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.SingleAsync(candidate => candidate.Email == "encyclopedia-pro-detail@test.com");
        player.ProSubscriptionEndsAtUtc = DateTime.UtcNow.AddDays(30);
        await db.SaveChangesAsync();

        var proResult = await ExecuteGraphQlAsync(
            """
            query EncyclopediaDetail($slug: String!) {
              encyclopediaResourceDetail(slug: $slug) {
                usedInRecipes {
                  output {
                    slug
                    isProOnly
                    isUnlockedForCurrentPlayer
                  }
                }
              }
            }
            """,
            new { slug = "silicon" },
            token);

        foreach (var recipe in proResult.GetProperty("data").GetProperty("encyclopediaResourceDetail").GetProperty("usedInRecipes").EnumerateArray())
        {
            var output = recipe.GetProperty("output");
            if (output.GetProperty("isProOnly").GetBoolean())
            {
                Assert.True(output.GetProperty("isUnlockedForCurrentPlayer").GetBoolean());
            }
        }
    }
}
