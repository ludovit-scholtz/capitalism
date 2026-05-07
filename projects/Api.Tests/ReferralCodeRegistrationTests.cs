using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the referral code system:
/// - Registration with a valid referral code persists the code on the player record.
/// - Registration without a referral code leaves AppliedReferralCode null.
/// - Invalid/malformed codes are silently ignored (normalised to null).
/// - Backend code length limits: 4–20 alphanumeric characters.
/// </summary>
public sealed class ReferralCodeRegistrationTests
{
    private static readonly string RegisterMutation = """
        mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token
            player { id email appliedReferralCode }
          }
        }
        """;

    private static readonly string GenerateReferralCodeMutation = """
        mutation GenerateReferralCode {
          generateReferralCode
        }
        """;

    private static readonly string CompleteOnboardingMutation = """
        mutation CompleteOnboarding($input: OnboardingInput!) {
          completeOnboarding(input: $input) {
            company { id }
          }
        }
        """;

    // -----------------------------------------------------------------------
    // 1. Registration without a referral code
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithoutReferralCode_LeavesAppliedReferralCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new { input = new { email = "noref@example.com", displayName = "No Ref", password = "TestPass123!" } });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);

        // Also verify via database
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbPlayer = await db.Players.FirstAsync(p => p.Email == "noref@example.com");
        Assert.Null(dbPlayer.AppliedReferralCode);
    }

    // -----------------------------------------------------------------------
    // 2. Registration WITH a valid referral code
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithValidReferralCode_PersistsNormalizedCodeOnPlayer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "withref@example.com",
                    displayName = "With Ref",
                    password = "TestPass123!",
                    referralCode = "ABC12345"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABC12345", player.GetProperty("appliedReferralCode").GetString());

        // Also verify via database
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbPlayer = await db.Players.FirstAsync(p => p.Email == "withref@example.com");
        Assert.Equal("ABC12345", dbPlayer.AppliedReferralCode);
    }

    // -----------------------------------------------------------------------
    // 3. Code is normalised: lowercase input → uppercase stored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithLowercaseReferralCode_StoresUppercase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "lowref@example.com",
                    displayName = "Low Ref",
                    password = "TestPass123!",
                    referralCode = "abc12345"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABC12345", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 4. Codes with whitespace are trimmed then validated
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithWhitespacePaddedReferralCode_StoresTrimmedUppercase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "trimref@example.com",
                    displayName = "Trim Ref",
                    password = "TestPass123!",
                    referralCode = "  REF99999  "
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("REF99999", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 5. Codes shorter than 4 characters are silently ignored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithTooShortReferralCode_LeavesAppliedCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "shortref@example.com",
                    displayName = "Short Ref",
                    password = "TestPass123!",
                    referralCode = "AB"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);
    }

    // -----------------------------------------------------------------------
    // 6. Codes with special characters are silently ignored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithSpecialCharReferralCode_LeavesAppliedCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "specialref@example.com",
                    displayName = "Special Ref",
                    password = "TestPass123!",
                    referralCode = "BAD!CODE"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);
    }

    // -----------------------------------------------------------------------
    // 7. Code at exactly 4 characters (lower boundary) is accepted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithFourCharReferralCode_IsAccepted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "fourref@example.com",
                    displayName = "Four Ref",
                    password = "TestPass123!",
                    referralCode = "ABCD"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABCD", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 8. Code at exactly 20 characters (upper boundary) is accepted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithTwentyCharReferralCode_IsAccepted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "twentyref@example.com",
                    displayName = "Twenty Ref",
                    password = "TestPass123!",
                    referralCode = "ABCDEFGHIJ1234567890"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABCDEFGHIJ1234567890", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 9. Code with 21 characters is silently ignored (over max)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithTwentyOneCharReferralCode_LeavesAppliedCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "longref@example.com",
                    displayName = "Long Ref",
                    password = "TestPass123!",
                    referralCode = "ABCDEFGHIJ12345678901"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);
    }

    // -----------------------------------------------------------------------
    // 10. Referral code field is returned in the `me` query
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Me_Query_ReturnsAppliedReferralCode()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register with a code
        var regResult = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "meref@example.com",
                    displayName = "Me Ref",
                    password = "TestPass123!",
                    referralCode = "MYCODE01"
                }
            });

        var token = regResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        // Query `me`
        var meResult = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "{ me { id appliedReferralCode } }",
            token: token);

        var mePlayer = meResult.GetProperty("data").GetProperty("me");
        Assert.Equal("MYCODE01", mePlayer.GetProperty("appliedReferralCode").GetString());
    }

    [Fact]
    public async Task GenerateReferralCode_IsIdempotentForSamePlayer_AndUniqueAcrossPlayers()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var firstRegister = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "ref-owner-1@example.com",
                    displayName = "Ref Owner 1",
                    password = "TestPass123!"
                }
            });
        var firstToken = firstRegister.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        var firstCodeResult = await TestHelpers.ExecuteGraphQlAsync(client, GenerateReferralCodeMutation, token: firstToken);
        var firstCode = firstCodeResult.GetProperty("data").GetProperty("generateReferralCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(firstCode));

        var secondCodeResult = await TestHelpers.ExecuteGraphQlAsync(client, GenerateReferralCodeMutation, token: firstToken);
        var secondCode = secondCodeResult.GetProperty("data").GetProperty("generateReferralCode").GetString();
        Assert.Equal(firstCode, secondCode);

        var secondRegister = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "ref-owner-2@example.com",
                    displayName = "Ref Owner 2",
                    password = "TestPass123!"
                }
            });
        var secondToken = secondRegister.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        var thirdCodeResult = await TestHelpers.ExecuteGraphQlAsync(client, GenerateReferralCodeMutation, token: secondToken);
        var thirdCode = thirdCodeResult.GetProperty("data").GetProperty("generateReferralCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(thirdCode));
        Assert.NotEqual(firstCode, thirdCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(2, await db.ReferralCodes.CountAsync());
    }

    [Fact]
    public async Task CompleteOnboarding_WithReferralCode_AppliesDiscountAndCreatesReferralRegistration()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerRegister = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "ref-owner-onboarding@example.com",
                    displayName = "Ref Owner Onboarding",
                    password = "TestPass123!"
                }
            });
        var ownerToken = ownerRegister.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var generatedCodeResult = await TestHelpers.ExecuteGraphQlAsync(client, GenerateReferralCodeMutation, token: ownerToken);
        var generatedCode = generatedCodeResult.GetProperty("data").GetProperty("generateReferralCode").GetString()!;

        var inviteeRegister = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "referred-onboarding@example.com",
                    displayName = "Referred Onboarding",
                    password = "TestPass123!",
                    referralCode = generatedCode
                }
            });
        var inviteeToken = inviteeRegister.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var inviteeId = Guid.Parse(inviteeRegister.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cityId = await db.Cities
                .AsNoTracking()
                .OrderBy(city => city.Population)
                .Select(city => city.Id)
                .FirstAsync();
            var productId = await db.ProductTypes
                .AsNoTracking()
                .Where(product => product.Slug == "wooden-chair")
                .Select(product => product.Id)
                .FirstAsync();

            await TestHelpers.ExecuteGraphQlAsync(
                client,
                CompleteOnboardingMutation,
                new
                {
                    input = new
                    {
                        industry = Industry.Furniture,
                        cityId,
                        productTypeId = productId,
                        companyName = "Referral Discount Co",
                    }
                },
                token: inviteeToken);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await verificationDb.Companies
            .AsNoTracking()
            .Where(candidate => candidate.PlayerId == inviteeId)
            .Select(candidate => new { candidate.Id, candidate.Name })
            .FirstAsync();
        var lotsByBuildingId = await verificationDb.BuildingLots
            .AsNoTracking()
            .Where(lot => lot.OwnerCompanyId == company.Id)
            .ToDictionaryAsync(lot => lot.BuildingId!.Value, lot => lot.Price);
        var propertyPurchases = await verificationDb.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.CompanyId == company.Id && entry.Category == LedgerCategory.PropertyPurchase)
            .ToListAsync();
        Assert.Equal(2, propertyPurchases.Count);

        foreach (var purchase in propertyPurchases)
        {
            Assert.NotNull(purchase.BuildingId);
            var lotPrice = lotsByBuildingId[purchase.BuildingId!.Value];
            var expectedDiscountedPrice = decimal.Round(-lotPrice * 0.9m, 2, MidpointRounding.AwayFromZero);
            Assert.Equal(expectedDiscountedPrice, purchase.Amount);
        }

        var registration = await verificationDb.ReferralRegistrations
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.ReferredPlayerId == inviteeId);
        Assert.NotNull(registration);

        var referralCode = await verificationDb.ReferralCodes.AsNoTracking().FirstAsync(candidate => candidate.Code == generatedCode);
        Assert.Equal(1, referralCode.UsageCount);
    }

    [Fact]
    public async Task CompleteOnboarding_WithUnknownReferralCode_ReturnsReferralCodeInvalidError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var inviteeRegister = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "referred-invalid-code@example.com",
                    displayName = "Referred Invalid",
                    password = "TestPass123!",
                    referralCode = "UNKNOWN99"
                }
            });
        var inviteeToken = inviteeRegister.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cityId = await db.Cities
            .AsNoTracking()
            .OrderBy(city => city.Population)
            .Select(city => city.Id)
            .FirstAsync();
        var productId = await db.ProductTypes
            .AsNoTracking()
            .Where(product => product.Slug == "wooden-chair")
            .Select(product => product.Id)
            .FirstAsync();

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            CompleteOnboardingMutation,
            new
            {
                input = new
                {
                    industry = Industry.Furniture,
                    cityId,
                    productTypeId = productId,
                    companyName = "Invalid Ref Co",
                }
            },
            token: inviteeToken);

        var errorCode = result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("REFERRAL_CODE_INVALID", errorCode);
    }
}
