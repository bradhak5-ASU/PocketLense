using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PocketLense.Infrastructure.Data;
using Testcontainers.PostgreSql;

namespace PocketLense.Tests.Integration;

public class ApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer database = new PostgreSqlBuilder("postgres:17.6").Build();
    private WebApplicationFactory<Program> factory = null!;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = database.GetConnectionString(),
                ["Jwt:SigningKey"] = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
                ["Ai:Enabled"] = "false"
            }));
        });
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await factory.DisposeAsync();
        await database.DisposeAsync();
    }

    private async Task<HttpClient> User()
    {
        var client = factory.CreateClient();
        string email = Guid.NewGuid() + "@example.com";
        string password = Guid.NewGuid().ToString("N") + "Aa9!";
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        login.EnsureSuccessStatusCode();
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.GetProperty("token").GetString());
        return client;
    }

    private static async Task<string> Account(HttpClient client)
    {
        var accounts = await client.GetFromJsonAsync<JsonElement>("/api/accounts");
        return accounts[0].GetProperty("id").GetString()!;
    }

    [Fact]
    public async Task RegistrationTransactionsAndUserIsolationWorkTogether()
    {
        using var first = await User();
        using var second = await User();
        var accountId = await Account(first);
        var create = await first.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-08-01",
            description = "Grocery run",
            amount = -45.25m,
            accountId
        });
        create.EnsureSuccessStatusCode();
        var item = await create.Content.ReadFromJsonAsync<JsonElement>();
        string id = item.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/transactions/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/transactions/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.DeleteAsync("/api/transactions/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-08-01",
            description = "Invalid owner",
            amount = -1,
            accountId
        })).StatusCode);
        var categories = await first.GetFromJsonAsync<JsonElement>("/api/categories");
        var categoryId = categories.EnumerateArray().First(c => c.GetProperty("kind").GetString() == "Expense").GetProperty("id").GetString();
        var budgetResponse = await first.PutAsJsonAsync("/api/budgets", new
        {
            categoryId,
            month = "2026-08-01",
            limit = 300
        });
        budgetResponse.EnsureSuccessStatusCode();
        var budget = await budgetResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/budgets/" + budget.GetProperty("id").GetString())).StatusCode);
        var subscriptionResponse = await first.PostAsJsonAsync("/api/subscriptions", new
        {
            displayName = "Netflix",
            amount = 15.49m,
            frequency = "Monthly",
            nextDate = "2026-09-03"
        });
        subscriptionResponse.EnsureSuccessStatusCode();
        var subscription = await subscriptionResponse.Content.ReadFromJsonAsync<JsonElement>();
        string subscriptionId = subscription.GetProperty("id").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync("/api/subscriptions/" + subscriptionId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await second.PostAsJsonAsync("/api/subscriptions/" + subscriptionId + "/dismiss", new
        {
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await first.DeleteAsync("/api/accounts/" + accountId)).StatusCode);
    }

    private static MultipartFormDataContent Upload(string accountId, string csv)
    {
        var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(csv)), "file", "statement.csv");
        content.Add(new StringContent(accountId), "accountId");
        content.Add(new StringContent(JsonSerializer.Serialize(new
        {
            dateColumn = "Date",
            dateFormat = "yyyy-MM-dd",
            descriptionColumn = "Description",
            amountColumn = "Amount",
            invertSign = false
        })), "mapping");
        return content;
    }

    [Fact]
    public async Task CsvImportIsAtomicAndDuplicateSafe()
    {
        using var client = await User();
        string account = await Account(client);
        const string csv = "Date,Description,Amount\n2026-08-01,Coffee,-4\n2026-08-01,Coffee,-4";
        using var upload = Upload(account, csv);
        var first = await client.PostAsync("/api/imports/commit", upload);
        first.EnsureSuccessStatusCode();
        Assert.Equal(2, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("imported").GetInt32());
        using var repeat = Upload(account, csv);
        var second = await client.PostAsync("/api/imports/commit", repeat);
        second.EnsureSuccessStatusCode();
        var result = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, result.GetProperty("imported").GetInt32());
        Assert.Equal(2, result.GetProperty("skippedDuplicates").GetInt32());
        using var bad = Upload(account, "Date,Description,Amount\n2026-08-02,New valid row,-7\nbad,Broken,-8");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/imports/commit", bad)).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>("/api/transactions");
        Assert.Equal(2, list.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task DismissedSubscriptionsAreNotSuggestedAgain()
    {
        using var client = await User();
        using var upload = Upload(await Account(client), "Date,Description,Amount\n2026-06-03,Netflix,-15.49\n2026-07-03,Netflix,-15.49\n2026-08-03,Netflix,-15.49");
        (await client.PostAsync("/api/imports/commit", upload)).EnsureSuccessStatusCode();
        var list = await client.GetFromJsonAsync<JsonElement>("/api/subscriptions");
        string id = list.GetProperty("items")[0].GetProperty("id").GetString()!;
        (await client.PostAsJsonAsync("/api/subscriptions/" + id + "/dismiss", new
        {
        })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync("/api/subscriptions/detect", new
        {
        })).EnsureSuccessStatusCode();
        list = await client.GetFromJsonAsync<JsonElement>("/api/subscriptions");
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());
        Assert.Equal("Dismissed", list.GetProperty("items")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task EmptyDashboardAndDisabledAiAreSafe()
    {
        using var client = await User();
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard?month=2026-08");
        Assert.Equal(0, dashboard.GetProperty("comparison").GetProperty("spending").GetDecimal());
        Assert.Empty(dashboard.GetProperty("recent").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/assistant/ask", new
        {
            question = "What did I spend?"
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/transactions", new
        {
            date = "2026-08-01",
            description = "Invalid zero",
            amount = 0,
            accountId = await Account(client)
        })).StatusCode);
    }
}
