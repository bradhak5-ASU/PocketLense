using PocketLense.Core.Models;

namespace PocketLense.Core.Services;

public static class CategoryMatcher
{
    public static Guid? Match(string description, IEnumerable<CategoryRule> rules)
    {
        return rules.OrderByDescending(rule => rule.Keyword.Length)
            .ThenBy(rule => rule.Id)
            .FirstOrDefault(rule => description.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))?.CategoryId;
    }
}
