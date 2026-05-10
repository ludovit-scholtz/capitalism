using Api.Data;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Api.Tests;

public sealed class FxDebugTest
{
    [Fact]
    public async Task Debug_ExecuteSwap_PrintsActualResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        
        // Register and login
        using var reg = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        reg.Content = new StringContent(JsonSerializer.Serialize(new { query = "mutation R($i: RegisterInput!) { register(input: $i) { token } }", variables = new { i = new { email = "dbg4@example.com", displayName = "DbgUser", password = "TestPass123!" } } }), System.Text.Encoding.UTF8, "application/json");
        var regResp = await client.SendAsync(reg);
        var regBody = await regResp.Content.ReadAsStringAsync();
        var regEl = JsonDocument.Parse(regBody).RootElement;
        var token = regEl.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        // Get player ID
        using var meReq = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        meReq.Content = new StringContent(JsonSerializer.Serialize(new { query = "{ me { id } }" }), System.Text.Encoding.UTF8, "application/json");
        meReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var meResp = await client.SendAsync(meReq);
        var meBody = await meResp.Content.ReadAsStringAsync();
        var meEl = JsonDocument.Parse(meBody).RootElement;
        var playerId = Guid.Parse(meEl.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
        
        // Fund the account with SaveChangesAsync
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.FirstAsync(p => p.Id == playerId);
        await PersonalBankAccountService.SetTrackedGrossCashAsync(db, player, 5000m);
        await db.SaveChangesAsync();
        
        // Get a quote
        using var quoteReq = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        quoteReq.Content = new StringContent(JsonSerializer.Serialize(new { query = "query Q($input: GetForexQuoteInput!) { forexQuote(input: $input) { quoteNonce rate } }", variables = new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m } } }), System.Text.Encoding.UTF8, "application/json");
        quoteReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var quoteResp = await client.SendAsync(quoteReq);
        var quoteBody = await quoteResp.Content.ReadAsStringAsync();
        var quoteEl = JsonDocument.Parse(quoteBody).RootElement;
        var nonce = quoteEl.GetProperty("data").GetProperty("forexQuote").GetProperty("quoteNonce").GetString();
        
        // Execute the swap
        using var swapReq = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        swapReq.Content = new StringContent(JsonSerializer.Serialize(new { query = "mutation M($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { tradeId fromCurrencyCode toAmount } }", variables = new { input = new { fromCurrencyCode = "EUR", toCurrencyCode = "CZK", amount = 100m, quoteNonce = nonce } } }), System.Text.Encoding.UTF8, "application/json");
        swapReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var swapResp = await client.SendAsync(swapReq);
        var swapBody = await swapResp.Content.ReadAsStringAsync();
        
        await System.IO.File.WriteAllTextAsync("/tmp/fx_swap_debug2.txt", $"QUOTE HTTP {(int)quoteResp.StatusCode}: {quoteBody}\nNonce: {nonce}\nSWAP HTTP {(int)swapResp.StatusCode}: {swapBody}");
        Assert.True(true);
    }
}
