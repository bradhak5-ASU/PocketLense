using Microsoft.EntityFrameworkCore;
using PocketLense.Core.Models;
using PocketLense.Core.Services;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Infrastructure.Services;

public class FinanceService(AppDbContext db)
{
    public async Task<List<BudgetStatus>> BudgetStatus(string userId, DateOnly month)
    {
        var budgets = await db.Budgets.Where(b => b.UserId == userId && b.Month == month).ToListAsync();
        var categories = await db.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id);
        var transactions = await MonthTransactions(userId, month);
        return budgets.Select(b => BudgetCalculator.Calculate(b, categories[b.CategoryId].Name,
            transactions.Where(t => t.CategoryId == b.CategoryId && t.Amount < 0).Sum(t => -t.Amount))).ToList();
    }

    public Task<List<Transaction>> MonthTransactions(string userId, DateOnly month)
    {
        var end = month.AddMonths(1);
        return db.Transactions.AsNoTracking().Where(t => t.UserId == userId && t.Date >= month && t.Date < end).ToListAsync();
    }

    public async Task<object> SpendingByCategory(string userId, DateOnly month)
    {
        var categories = await db.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id);
        var transactions = await MonthTransactions(userId, month);
        return transactions.Where(t => IsExpense(t, categories)).GroupBy(t => t.CategoryId)
            .Select(g => new { category = g.Key.HasValue ? categories[g.Key.Value].Name : "Uncategorized", amount = g.Sum(t => -t.Amount) })
            .OrderByDescending(x => x.amount).ToList();
    }

    public async Task<object> MonthComparison(string userId, DateOnly month)
    {
        var categories = await db.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id);
        decimal current = (await MonthTransactions(userId, month)).Where(t => IsExpense(t, categories)).Sum(t => -t.Amount);
        decimal previous = (await MonthTransactions(userId, month.AddMonths(-1))).Where(t => IsExpense(t, categories)).Sum(t => -t.Amount);
        decimal? percentChange = previous == 0 ? null : Math.Round((current - previous) / previous * 100, 1);
        return new
        {
            spending = current,
            previousSpending = previous,
            percentChange
        };
    }

    public async Task<object> Dashboard(string userId, DateOnly month)
    {
        var categories = await db.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id);
        var transactions = await MonthTransactions(userId, month);
        var trends = new List<object>();
        for (int i = 5; i >= 0; i--)
        {
            var start = month.AddMonths(-i);
            var items = await MonthTransactions(userId, start);
            trends.Add(new
            {
                month = start.ToString("yyyy-MM"),
                amount = items.Where(t => IsExpense(t, categories)).Sum(t => -t.Amount)
            });
        }
        var subscriptions = await db.Subscriptions.Where(s => s.UserId == userId && s.Status == "Confirmed").ToListAsync();
        return new
        {
            comparison = await MonthComparison(userId, month),
            income = transactions.Where(t => t.Amount > 0 && (!t.CategoryId.HasValue || categories[t.CategoryId.Value].Kind == "Income")).Sum(t => t.Amount),
            monthlySubscriptions = Math.Round(subscriptions.Sum(SubscriptionDetector.MonthlyCost), 2),
            categories = await SpendingByCategory(userId, month),
            trends,
            budgets = await BudgetStatus(userId, month),
            upcoming = await UpcomingBills(userId, 7),
            recent = transactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).Take(5)
                .Select(t => new { t.Id, t.Date, t.Description, t.Amount, category = t.CategoryId.HasValue ? categories[t.CategoryId.Value].Name : "Uncategorized" })
        };
    }

    public async Task<List<object>> UpcomingBills(string userId, int days)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var end = today.AddDays(Math.Clamp(days, 1, 90));
        var items = await db.Subscriptions.AsNoTracking().Where(s => s.UserId == userId && s.Status == "Confirmed").ToListAsync();
        var upcoming = new List<(DateOnly Date, object Bill)>();
        foreach (var item in items)
        {
            var date = item.NextDate;
            if (date == default)
                continue;
            while (date < today)
                date = SubscriptionDetector.NextDate(date, item.Frequency);
            while (date < end)
            {
                upcoming.Add((date, new
                {
                    item.Id,
                    item.DisplayName,
                    item.Amount,
                    nextDate = date
                }));
                date = SubscriptionDetector.NextDate(date, item.Frequency);
            }
        }
        return upcoming.OrderBy(x => x.Date).Select(x => x.Bill).ToList();
    }

    public async Task DetectSubscriptions(string userId)
    {
        var excluded = await db.Categories.Where(c => c.UserId == userId && c.Kind != "Expense").Select(c => c.Id).ToListAsync();
        var transactions = await db.Transactions.Where(t => t.UserId == userId && (!t.CategoryId.HasValue || !excluded.Contains(t.CategoryId.Value))).ToListAsync();
        var saved = await db.Subscriptions.Where(s => s.UserId == userId && !s.IsManual).ToListAsync();
        foreach (var candidate in SubscriptionDetector.Detect(transactions))
        {
            var existing = saved.FirstOrDefault(s => s.MerchantKey == candidate.MerchantKey);
            if (existing == null)
                db.Subscriptions.Add(candidate);
            else if (existing.Status != "Dismissed")
            {
                existing.Amount = candidate.Amount;
                existing.PreviousAmount = candidate.PreviousAmount;
                existing.NextDate = candidate.NextDate;
                existing.Frequency = candidate.Frequency;
            }
        }
        await db.SaveChangesAsync();
    }

    private static bool IsExpense(Transaction transaction, Dictionary<Guid, Category> categories)
    {
        // I exclude transfers so moving money between accounts doesn't count as spending.
        return transaction.Amount < 0 && (!transaction.CategoryId.HasValue || categories[transaction.CategoryId.Value].Kind == "Expense");
    }
}
