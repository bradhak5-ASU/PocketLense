using PocketLense.Core.Models;

namespace PocketLense.Core.Services;

public record BudgetStatus(Guid Id, Guid CategoryId, string Category, decimal Limit,
    decimal Spent, decimal Remaining, decimal PercentUsed, string Status);

public static class BudgetCalculator
{
    public static BudgetStatus Calculate(Budget budget, string category, decimal spent)
    {
        decimal percent = budget.Limit > 0 ? spent / budget.Limit * 100 : 0;
        string status = percent >= 100 ? "Over" : percent >= 80 ? "Warning" : "OnTrack";
        return new(budget.Id, budget.CategoryId, category, budget.Limit, spent,
            budget.Limit - spent, Math.Round(percent, 2), status);
    }
}
