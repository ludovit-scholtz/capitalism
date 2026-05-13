using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Integration tests for the Energy Spot Market feature.
/// </summary>
public sealed class EnergySpotMarketTests
{
    private static async Task<JsonElement> ExecuteAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8, "application/json"),
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var result = await ExecuteAsync(client,
            "mutation R($i:RegisterInput!){register(input:$i){token}}",
            new { i = new { email, displayName = "Tester", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static City MakeCity(AppDbContext db)
    {
        var c = new City { Id = Guid.NewGuid(), Name = $"EC{Guid.NewGuid():N[..6]}", CountryCode = "TS",
            Population = 500_000, Latitude = 48.0, Longitude = 17.0, CurrencyCode = "EUR",
            AverageRentPerSqm = 10m, BaseSalaryPerManhour = 10m };
        db.Cities.Add(c); return c;
    }
    private static Building MakePlant(AppDbContext db, Guid cityId, Guid companyId)
    {
        var b = new Building { Id = Guid.NewGuid(), Name = $"P{Guid.NewGuid():N[..4]}", Type = BuildingType.PowerPlant,
            CityId = cityId, CompanyId = companyId, Latitude = 48.1, Longitude = 17.1,
            PowerStatus = PowerStatus.Powered, PowerPlantType = "COAL", PowerOutput = 50m };
        db.Buildings.Add(b); return b;
    }
    private static Building MakeConsumer(AppDbContext db, Guid cityId, Guid companyId, decimal? maxBid)
    {
        var b = new Building { Id = Guid.NewGuid(), Name = $"C{Guid.NewGuid():N[..4]}", Type = BuildingType.Factory,
            CityId = cityId, CompanyId = companyId, Latitude = 48.2, Longitude = 17.2,
            PowerStatus = PowerStatus.Offline, PowerConsumption = 5m, MaxEnergyBidPrice = maxBid };
        db.Buildings.Add(b); return b;
    }

    // ---- listEnergyForSale -------------------------------------------------------

    [Fact]
    public async Task ListEnergyForSale_ByOwner_CreatesActiveListing()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"les-{Guid.NewGuid():N}@t.com";
        var token = await RegisterAsync(client, email);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var player = await db.Players.FirstAsync(p => p.Email == email);
        var co = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Co" };
        db.Companies.Add(co);
        var plant = MakePlant(db, city.Id, co.Id);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            "mutation L($i:ListEnergyForSaleInput!){listEnergyForSale(input:$i){id buildingId pricePerKwhLocal capacityKw}}",
            new { i = new { buildingId = plant.Id.ToString(), pricePerKwhLocal = 0.05m, capacityKw = 200m } }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var data = result.GetProperty("data").GetProperty("listEnergyForSale");
        Assert.Equal(plant.Id.ToString().ToLower(), data.GetProperty("buildingId").GetString()!.ToLower());
        Assert.Equal(0.05m, data.GetProperty("pricePerKwhLocal").GetDecimal());
        Assert.Equal(200m, data.GetProperty("capacityKw").GetDecimal());

        var dbListing = await db.EnergyListings.FirstOrDefaultAsync(l => l.BuildingId == plant.Id && l.IsActive);
        Assert.NotNull(dbListing);
    }

    [Fact]
    public async Task ListEnergyForSale_ForeignBuilding_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, $"lff-{Guid.NewGuid():N}@t.com");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var op = new Player { Id = Guid.NewGuid(), Email = $"op-{Guid.NewGuid():N}@t.com", DisplayName = "Op", PasswordHash = "h", Role = PlayerRole.Player };
        var oc = new Company { Id = Guid.NewGuid(), PlayerId = op.Id, Name = "OC" };
        db.Players.Add(op); db.Companies.Add(oc);
        var plant = MakePlant(db, city.Id, oc.Id);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            "mutation L($i:ListEnergyForSaleInput!){listEnergyForSale(input:$i){id}}",
            new { i = new { buildingId = plant.Id.ToString(), pricePerKwhLocal = 0.05m, capacityKw = 100m } }, token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Equal("NOT_FOUND_OR_NOT_OWNED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ListEnergyForSale_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var result = await ExecuteAsync(client,
            "mutation L($i:ListEnergyForSaleInput!){listEnergyForSale(input:$i){id}}",
            new { i = new { buildingId = Guid.NewGuid().ToString(), pricePerKwhLocal = 0.05m, capacityKw = 100m } });
        Assert.True(result.TryGetProperty("errors", out _));
    }

    // ---- cancelEnergyListing ----------------------------------------------------

    [Fact]
    public async Task CancelEnergyListing_ByOwner_DeactivatesListing()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"cel-{Guid.NewGuid():N}@t.com";
        var token = await RegisterAsync(client, email);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var player = await db.Players.FirstAsync(p => p.Email == email);
        var co = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "CancelCo" };
        db.Companies.Add(co);
        var plant = MakePlant(db, city.Id, co.Id);
        await db.SaveChangesAsync();

        var listResult = await ExecuteAsync(client,
            "mutation L($i:ListEnergyForSaleInput!){listEnergyForSale(input:$i){id}}",
            new { i = new { buildingId = plant.Id.ToString(), pricePerKwhLocal = 0.05m, capacityKw = 100m } }, token);
        Assert.False(listResult.TryGetProperty("errors", out _), listResult.ToString());
        var listingIdStr = listResult.GetProperty("data").GetProperty("listEnergyForSale").GetProperty("id").GetString()!;
        var listingId = Guid.Parse(listingIdStr);

        var cancelResult = await ExecuteAsync(client,
            "mutation C($i:CancelEnergyListingInput!){cancelEnergyListing(input:$i){id isActive}}",
            new { i = new { listingId = listingIdStr } }, token);
        Assert.False(cancelResult.TryGetProperty("errors", out _), cancelResult.ToString());

        var dbListing = await db.EnergyListings.FindAsync(listingId);
        Assert.NotNull(dbListing);
        Assert.False(dbListing.IsActive);
        Assert.NotNull(dbListing.CancelledAtUtc);
    }

    [Fact]
    public async Task CancelEnergyListing_ForeignListing_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, $"cfl-{Guid.NewGuid():N}@t.com");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var op = new Player { Id = Guid.NewGuid(), Email = $"op2-{Guid.NewGuid():N}@t.com", DisplayName = "Op2", PasswordHash = "h", Role = PlayerRole.Player };
        var oc = new Company { Id = Guid.NewGuid(), PlayerId = op.Id, Name = "OC2" };
        db.Players.Add(op); db.Companies.Add(oc);
        var plant = MakePlant(db, city.Id, oc.Id);
        var listing = new EnergyListing { Id = Guid.NewGuid(), BuildingId = plant.Id, CompanyId = oc.Id,
            CityId = city.Id, PricePerKwhLocal = 0.05m, CapacityKw = 100m, AvailableKw = 100m, IsActive = true, CreatedAtTick = 0 };
        db.EnergyListings.Add(listing);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            "mutation C($i:CancelEnergyListingInput!){cancelEnergyListing(input:$i){id}}",
            new { i = new { listingId = listing.Id.ToString() } }, token);
        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Equal("NOT_FOUND_OR_NOT_OWNED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ---- setMaxEnergyBidPrice ---------------------------------------------------

    [Fact]
    public async Task SetMaxEnergyBidPrice_ByOwner_UpdatesBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"smb-{Guid.NewGuid():N}@t.com";
        var token = await RegisterAsync(client, email);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var player = await db.Players.FirstAsync(p => p.Email == email);
        var co = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "BidCo" };
        db.Companies.Add(co);
        var bldg = MakeConsumer(db, city.Id, co.Id, null);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            "mutation S($i:SetMaxEnergyBidPriceInput!){setMaxEnergyBidPrice(input:$i){id maxEnergyBidPrice}}",
            new { i = new { buildingId = bldg.Id.ToString(), maxBidPricePerKwh = 0.08m } }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        Assert.Equal(0.08m, result.GetProperty("data").GetProperty("setMaxEnergyBidPrice").GetProperty("maxEnergyBidPrice").GetDecimal());

        await db.Entry(bldg).ReloadAsync();
        Assert.Equal(0.08m, bldg.MaxEnergyBidPrice);
    }

    [Fact]
    public async Task SetMaxEnergyBidPrice_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var result = await ExecuteAsync(client,
            "mutation S($i:SetMaxEnergyBidPriceInput!){setMaxEnergyBidPrice(input:$i){id}}",
            new { i = new { buildingId = Guid.NewGuid().ToString(), maxBidPricePerKwh = 0.05m } });
        Assert.True(result.TryGetProperty("errors", out _));
    }

    // ---- energyMarket query -----------------------------------------------------

    [Fact]
    public async Task EnergyMarketQuery_ReturnsActiveListings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var op = new Player { Id = Guid.NewGuid(), Email = $"emq-{Guid.NewGuid():N}@t.com", DisplayName = "EMQ", PasswordHash = "h", Role = PlayerRole.Player };
        var co = new Company { Id = Guid.NewGuid(), PlayerId = op.Id, Name = "QCo" };
        db.Players.Add(op); db.Companies.Add(co);
        var plant = MakePlant(db, city.Id, co.Id);
        var listing = new EnergyListing { Id = Guid.NewGuid(), BuildingId = plant.Id, CompanyId = co.Id,
            CityId = city.Id, PricePerKwhLocal = 0.07m, CapacityKw = 500m, AvailableKw = 500m, IsActive = true, CreatedAtTick = 0 };
        db.EnergyListings.Add(listing);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            "query Q($cityId:UUID!){energyMarket(cityId:$cityId){listingId pricePerKwhLocal capacityKw}}",
            new { cityId = city.Id.ToString() });

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var arr = result.GetProperty("data").GetProperty("energyMarket");
        Assert.True(arr.GetArrayLength() >= 1);
        var found = arr.EnumerateArray().FirstOrDefault(l =>
            string.Equals(l.GetProperty("listingId").GetString(), listing.Id.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual(default, found);
        Assert.Equal(0.07m, found.GetProperty("pricePerKwhLocal").GetDecimal());
    }

    [Fact]
    public async Task EnergyMarketQuery_ExcludesCancelledListings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = MakeCity(db);
        var op = new Player { Id = Guid.NewGuid(), Email = $"emc-{Guid.NewGuid():N}@t.com", DisplayName = "EMC", PasswordHash = "h", Role = PlayerRole.Player };
        var co = new Company { Id = Guid.NewGuid(), PlayerId = op.Id, Name = "CCo" };
        db.Players.Add(op); db.Companies.Add(co);
        var plant = MakePlant(db, city.Id, co.Id);
        var cancelled = new EnergyListing { Id = Guid.NewGuid(), BuildingId = plant.Id, CompanyId = co.Id,
            CityId = city.Id, PricePerKwhLocal = 0.09m, CapacityKw = 100m, AvailableKw = 100m,
            IsActive = false, CancelledAtUtc = DateTime.UtcNow, CreatedAtTick = 0 };
        db.EnergyListings.Add(cancelled);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            "query Q($cityId:UUID!){energyMarket(cityId:$cityId){listingId}}",
            new { cityId = city.Id.ToString() });

        var arr = result.GetProperty("data").GetProperty("energyMarket");
        Assert.False(arr.EnumerateArray().Any(l =>
            string.Equals(l.GetProperty("listingId").GetString(), cancelled.Id.ToString(), StringComparison.OrdinalIgnoreCase)));
    }

    // ---- tick phase tests -------------------------------------------------------

    [Fact]
    public async Task EnergySpotMarketPhase_MatchesDeficitBuilding_ToAffordableListing()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = MakeCity(db);
        var sp = new Player { Id = Guid.NewGuid(), Email = $"sp-{Guid.NewGuid():N}@t.com", DisplayName = "SP", PasswordHash = "h", Role = PlayerRole.Player };
        var sc = new Company { Id = Guid.NewGuid(), PlayerId = sp.Id, Name = "SC" };
        db.Players.Add(sp); db.Companies.Add(sc);
        MakePlant(db, city.Id, sc.Id);

        var bp = new Player { Id = Guid.NewGuid(), Email = $"bp-{Guid.NewGuid():N}@t.com", DisplayName = "BP", PasswordHash = "h", Role = PlayerRole.Player };
        var bc = new Company { Id = Guid.NewGuid(), PlayerId = bp.Id, Name = "BC" };
        db.Players.Add(bp); db.Companies.Add(bc);
        var consumer = MakeConsumer(db, city.Id, bc.Id, 0.10m);
        var plant = await db.Buildings.Where(b => b.CompanyId == sc.Id).FirstAsync();
        db.BankAccounts.Add(new BankAccount { Id = Guid.NewGuid(), AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR", CompanyId = bc.Id, Balance = 50_000m, CreatedAtUtc = DateTime.UtcNow });

        var listing = new EnergyListing { Id = Guid.NewGuid(), BuildingId = plant.Id, CompanyId = sc.Id,
            CityId = city.Id, PricePerKwhLocal = 0.05m, CapacityKw = 1000m, AvailableKw = 1000m, IsActive = true, CreatedAtTick = 0 };
        db.EnergyListings.Add(listing);

        if (!await db.GameStates.AnyAsync())
            db.GameStates.Add(new GameState { Id = 1, CurrentTick = 1, TaxCycleTicks = 8760 });

        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(), NullLogger<TickProcessor>.Instance);
        await processor.ProcessTickAsync();

        var updated = await db.Buildings.FindAsync(consumer.Id);
        Assert.Equal(PowerStatus.Powered, updated!.PowerStatus);

        var sellerEntry = await db.LedgerEntries.FirstOrDefaultAsync(e => e.CompanyId == sc.Id && e.Category == LedgerCategory.EnergyRevenue);
        Assert.NotNull(sellerEntry);
        Assert.True(sellerEntry.Amount > 0m);

        var buyerEntry = await db.LedgerEntries.FirstOrDefaultAsync(e => e.CompanyId == bc.Id && e.Category == LedgerCategory.SpotMarketEnergyCost);
        Assert.NotNull(buyerEntry);
        Assert.True(buyerEntry.Amount < 0m);
    }

    [Fact]
    public async Task EnergySpotMarketPhase_DoesNotMatch_WhenBidPriceTooLow()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = MakeCity(db);
        var sp = new Player { Id = Guid.NewGuid(), Email = $"sp2-{Guid.NewGuid():N}@t.com", DisplayName = "SP2", PasswordHash = "h", Role = PlayerRole.Player };
        var sc = new Company { Id = Guid.NewGuid(), PlayerId = sp.Id, Name = "SC2" };
        db.Players.Add(sp); db.Companies.Add(sc);
        var plant = MakePlant(db, city.Id, sc.Id);

        var bp = new Player { Id = Guid.NewGuid(), Email = $"bp2-{Guid.NewGuid():N}@t.com", DisplayName = "BP2", PasswordHash = "h", Role = PlayerRole.Player };
        var bc = new Company { Id = Guid.NewGuid(), PlayerId = bp.Id, Name = "BC2" };
        db.Players.Add(bp); db.Companies.Add(bc);
        var consumer = MakeConsumer(db, city.Id, bc.Id, 0.01m); // bid too low
        db.BankAccounts.Add(new BankAccount { Id = Guid.NewGuid(), AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR", CompanyId = bc.Id, Balance = 50_000m, CreatedAtUtc = DateTime.UtcNow });

        var listing = new EnergyListing { Id = Guid.NewGuid(), BuildingId = plant.Id, CompanyId = sc.Id,
            CityId = city.Id, PricePerKwhLocal = 0.05m, CapacityKw = 1000m, AvailableKw = 1000m, IsActive = true, CreatedAtTick = 0 };
        db.EnergyListings.Add(listing);

        if (!await db.GameStates.AnyAsync())
            db.GameStates.Add(new GameState { Id = 1, CurrentTick = 1, TaxCycleTicks = 8760 });

        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(), NullLogger<TickProcessor>.Instance);
        await processor.ProcessTickAsync();

        var updated = await db.Buildings.FindAsync(consumer.Id);
        Assert.Equal(PowerStatus.Offline, updated!.PowerStatus);

        var sellerEntry = await db.LedgerEntries.FirstOrDefaultAsync(e => e.CompanyId == sc.Id && e.Category == LedgerCategory.EnergyRevenue);
        Assert.Null(sellerEntry);
    }
}
