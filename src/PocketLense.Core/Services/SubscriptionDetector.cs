using System.Text.RegularExpressions;
using PocketLense.Core.Models;

namespace PocketLense.Core.Services;

public static class SubscriptionDetector
{
    public static string MerchantKey(string description)
    {
        string letters = Regex.Replace(description.ToUpperInvariant(), @"[^\p{L}\s]", "");
        return Regex.Replace(letters, @"\s+", " ").Trim();
    }

    public static decimal MonthlyCost(Subscription item)
    {
        return item.Frequency switch
        {
            "Weekly" => item.Amount * 52 / 12,
            "Annual" => item.Amount / 12,
            _ => item.Amount
        };
    }

    public static DateOnly NextDate(DateOnly date, string frequency)
    {
        return frequency switch
        {
            "Weekly" => date.AddDays(7),
            "Annual" => date.AddYears(1),
            _ => date.AddMonths(1)
        };
    }

    public static List<Subscription> Detect(IEnumerable<Transaction> transactions)
    {
        var results = new List<Subscription>();
        foreach (var group in transactions.Where(t => t.Amount < 0).GroupBy(t => MerchantKey(t.Description)))
        {
            var charges = group.OrderBy(t => t.Date).ToList();
            if (charges.Count < 2 || group.Key.Length == 0)
                continue;
            var gaps = charges.Zip(charges.Skip(1), (a, b) => (decimal)(b.Date.DayNumber - a.Date.DayNumber));
            decimal gap = Median(gaps);
            string? frequency = gap >= 6 && gap <= 8 ? "Weekly"
                : gap >= 26 && gap <= 35 ? "Monthly"
                : gap >= 350 && gap <= 380 ? "Annual" : null;
            if (frequency == null || (frequency != "Annual" && charges.Count < 3))
                continue;
            decimal median = Median(charges.Select(t => Math.Abs(t.Amount)));
            if (charges.Any(t => Math.Abs(Math.Abs(t.Amount) - median) > median * 0.10m))
                continue;
            decimal latest = Math.Abs(charges[^1].Amount);
            decimal previous = Math.Abs(charges[^2].Amount);
            results.Add(new Subscription
            {
                UserId = charges[0].UserId,
                MerchantKey = group.Key,
                DisplayName = charges[^1].Description,
                Amount = latest,
                PreviousAmount = latest > previous * 1.01m ? previous : null,
                Frequency = frequency,
                NextDate = NextDate(charges[^1].Date, frequency)
            });
        }
        return results;
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Order().ToArray();
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
    }
}
