using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace PocketLense.Core.Services;

public static class ImportIdentity
{
    public static string Create(Guid accountId, DateOnly date, decimal amount, string description, int occurrence)
    {
        string normalized = Regex.Replace(description.Trim().ToUpperInvariant(), @"\s+", " ");
        string value = $"{accountId}|{date:yyyy-MM-dd}|{amount.ToString("F2", CultureInfo.InvariantCulture)}|{normalized}|{occurrence}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
