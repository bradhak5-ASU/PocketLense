using PocketLense.Core.Models;
using PocketLense.Core.Services;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Tests.Unit;

public class FinanceTests
{
    [Theory]
    [InlineData("79.99", "OnTrack")]
    [InlineData("80", "Warning")]
    [InlineData("99.99", "Warning")]
    [InlineData("100", "Over")]
    public void BudgetStatusChangesAtTheLimit(string spent, string expected)
    {
        var budget = new Budget { Limit = 100 };
        var result = BudgetCalculator.Calculate(budget, "Dining", decimal.Parse(spent, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void LongestRuleWinsRegardlessOfCase()
    {
        var shopping = new CategoryRule { Keyword = "AMAZON", CategoryId = Guid.NewGuid() };
        var subscriptions = new CategoryRule { Keyword = "AMAZON PRIME", CategoryId = Guid.NewGuid() };
        Assert.Equal(subscriptions.CategoryId, CategoryMatcher.Match("amazon prime*2K4", [shopping, subscriptions]));
        Assert.Null(CategoryMatcher.Match("Unrelated merchant", [shopping]));
    }

    [Theory]
    [InlineData(false, "-12.50")]
    [InlineData(true, "12.50")]
    public void CsvSingleAmountSupportsInvertedSigns(bool invert, string expected)
    {
        var mapping = new CsvMapping("Date", "yyyy-MM-dd", "Description", "Amount", null, null, invert);
        var result = CsvImportService.Parse("Date,Description,Amount\n2026-08-01,Coffee,-12.50", Guid.NewGuid(), mapping);
        Assert.Empty(result.Errors);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Assert.Single(result.Transactions).Amount);
    }

    [Fact]
    public void CsvDebitIsNegativeAndCreditIsPositive()
    {
        var mapping = new CsvMapping("Date", "yyyy-MM-dd", "Description", null, "Debit", "Credit", false);
        var result = CsvImportService.Parse("Date,Description,Debit,Credit\n2026-08-01,Groceries,45,\n2026-08-02,Salary,,200", Guid.NewGuid(), mapping);
        Assert.Empty(result.Errors);
        Assert.Equal(-45m, result.Transactions[0].Amount);
        Assert.Equal(200m, result.Transactions[1].Amount);
    }

    [Fact]
    public void CsvReportsBadDateWithItsRowNumber()
    {
        var mapping = new CsvMapping("Date", "yyyy-MM-dd", "Description", "Amount", null, null, false);
        var result = CsvImportService.Parse("Date,Description,Amount\n2026-08-01,Coffee,-4\nbad,Coffee,-4", Guid.NewGuid(), mapping);
        Assert.Equal(3, Assert.Single(result.Errors).Row);
    }

    [Fact]
    public void IdenticalPurchasesKeepSeparateRepeatableHashes()
    {
        var mapping = new CsvMapping("Date", "yyyy-MM-dd", "Description", "Amount", null, null, false);
        const string csv = "Date,Description,Amount\n2026-08-01,Coffee,-4\n2026-08-01,Coffee,-4";
        var account = Guid.NewGuid();
        var first = CsvImportService.Parse(csv, account, mapping).Transactions;
        var second = CsvImportService.Parse(csv, account, mapping).Transactions;
        Assert.NotEqual(first[0].ImportHash, first[1].ImportHash);
        Assert.Equal(first.Select(t => t.ImportHash), second.Select(t => t.ImportHash));
    }

    [Theory]
    [InlineData("Netflix", "15.49", "15.49", "15.49", false)]
    [InlineData("Spotify", "11.99", "11.99", "12.99", true)]
    public void MonthlySubscriptionsDetectPriceChanges(string name, string first, string second, string third, bool increased)
    {
        decimal Parse(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var items = new[] { Charge(name, new(2026, 6, 3), Parse(first)), Charge(name, new(2026, 7, 3), Parse(second)), Charge(name, new(2026, 8, 3), Parse(third)) };
        var subscription = Assert.Single(SubscriptionDetector.Detect(items));
        Assert.Equal("Monthly", subscription.Frequency);
        Assert.Equal(new DateOnly(2026, 9, 3), subscription.NextDate);
        Assert.Equal(increased, subscription.PreviousAmount.HasValue);
    }

    [Fact]
    public void AnnualSubscriptionsNeedTwoCharges()
    {
        var item = Assert.Single(SubscriptionDetector.Detect([Charge("Annual plan", new(2025, 1, 1), 99), Charge("Annual plan", new(2026, 1, 1), 99)]));
        Assert.Equal("Annual", item.Frequency);
        Assert.Equal(8.25m, SubscriptionDetector.MonthlyCost(item));
    }

    [Fact]
    public void WeeklySubscriptionsNeedThreeCharges()
    {
        var items = new[] { Charge("Weekly plan", new(2026, 1, 1), 10), Charge("Weekly plan", new(2026, 1, 8), 10), Charge("Weekly plan", new(2026, 1, 15), 10) };
        Assert.Equal("Weekly", Assert.Single(SubscriptionDetector.Detect(items)).Frequency);
        Assert.Empty(SubscriptionDetector.Detect(items.Take(2)));
    }

    [Fact]
    public void UnrelatedUberRidesDoNotBecomeSubscriptions()
    {
        Assert.Empty(SubscriptionDetector.Detect([Charge("UBER TRIP", new(2026, 6, 1), 15), Charge("UBER TRIP", new(2026, 6, 22), 16)]));
    }

    private static Transaction Charge(string description, DateOnly date, decimal amount) => new() { Description = description, Date = date, Amount = -amount, UserId = "test" };
}
