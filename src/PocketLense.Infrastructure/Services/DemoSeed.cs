using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PocketLense.Core.Models;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Infrastructure.Services;

public static class DemoSeed
{
    public static async Task Run(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<IdentityUser>>();
        const string email = "demo@pocketlense.dev";
        if (await users.FindByEmailAsync(email) != null)
            return;
        var password = configuration["Demo:Password"] ?? throw new InvalidOperationException("Set Demo:Password in user secrets before seeding.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("The demo password must meet the registration password requirements.");
        var account = new Account { UserId = user.Id, Name = "Checking" };
        db.Accounts.Add(account);
        string[] names = ["Housing", "Utilities", "Groceries", "Dining", "Transport", "Shopping", "Entertainment", "Subscriptions", "Health", "Other", "Income", "Transfers"];
        var categories = names.ToDictionary(name => name, name => new Category { UserId = user.Id, Name = name, Kind = name == "Income" ? "Income" : name == "Transfers" ? "Transfer" : "Expense" });
        db.Categories.AddRange(categories.Values);
        var month = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        for (int i = 2; i >= 0; i--)
        {
            var start = month.AddMonths(-i);
            void Add(int day, string description, decimal amount, string category)
            {
                db.Transactions.Add(new Transaction { UserId = user.Id, AccountId = account.Id, Date = start.AddDays(day - 1), Description = description, Amount = amount, CategoryId = categories[category].Id });
            }
            Add(1, "Salary deposit", 4850, "Income");
            Add(1, "Monthly rent", -1450, "Housing");
            Add(2, "Whole Foods Market", -87.40m - i * 8, "Groceries");
            Add(3, "Netflix", -15.49m, "Subscriptions");
            Add(4, "Spotify", i == 0 ? -12.99m : -11.99m, "Subscriptions");
            Add(5, "City Gym", -35, "Subscriptions");
            Add(5, "Electric bill", -78.60m, "Utilities");
            Add(6, "Weekend dinner", -64.50m, "Dining");
            Add(7, "UBER TRIP", -23.80m, "Transport");
            Add(8, "Transfer to savings", -400, "Transfers");
            Add(9, "Neighborhood cafe", -18.50m, "Dining");
            foreach (var (category, limit) in new[] { ("Groceries", 300m), ("Dining", 100m), ("Housing", 1400m), ("Transport", 120m) })
                db.Budgets.Add(new Budget { UserId = user.Id, CategoryId = categories[category].Id, Month = start, Limit = limit });
        }
        db.Rules.Add(new CategoryRule { UserId = user.Id, Keyword = "UBER", CategoryId = categories["Transport"].Id });
        await db.SaveChangesAsync();
        await services.GetRequiredService<FinanceService>().DetectSubscriptions(user.Id);
        foreach (var item in await db.Subscriptions.Where(s => s.UserId == user.Id).ToListAsync())
        if (item.DisplayName is "Netflix" or "Spotify" or "City Gym")
            item.Status = "Confirmed";
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
