using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using PocketLense.Core;
using PocketLense.Core.Models;
using PocketLense.Core.Services;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Infrastructure.Services;

public record CsvMapping(string DateColumn, string DateFormat, string DescriptionColumn,
    string? AmountColumn, string? DebitColumn, string? CreditColumn, bool InvertSign);
public record ImportError(int Row, string Reason);
public record ParsedCsv(List<Transaction> Transactions, List<ImportError> Errors);
public record ImportResult(int Imported, int SkippedDuplicates, List<ImportError> Errors);

public class CsvTransactionSource(string text, Guid accountId, CsvMapping mapping) : ITransactionSource
{
    public string Name => "Csv";
    public Task<List<Transaction>> ReadAsync(CancellationToken cancellationToken = default)
    {
        var result = CsvImportService.Parse(text, accountId, mapping);
        if (result.Errors.Count > 0)
            throw new AppException(400, "The CSV contains invalid rows.");
        return Task.FromResult(result.Transactions);
    }
}

public class ManualTransactionSource(Transaction transaction) : ITransactionSource
{
    public string Name => "Manual";
    public Task<List<Transaction>> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<Transaction> { transaction });
}

public class EmailTransactionSource : ITransactionSource
{
    public string Name => "Email";
    public Task<List<Transaction>> ReadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException("Email sync is not available yet.");
}

public class CsvImportService(AppDbContext db, FinanceService finance)
{
    private static CsvReader Reader(string text) => new(new StringReader(text), new CsvConfiguration(CultureInfo.InvariantCulture) { TrimOptions = TrimOptions.Trim });

    public static object Preview(string text)
    {
        using var csv = Reader(text);
        if (!csv.Read())
            throw new AppException(400, "The CSV is empty.");
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        CheckHeaders(headers);
        var rows = new List<string[]>();
        while (rows.Count < 20 && csv.Read())
            rows.Add(headers.Select((_, i) => csv.GetField(i) ?? "").ToArray());
        return new
        {
            headers,
            rows
        };
    }

    private static void CheckHeaders(string[] headers)
    {
        if (headers.Length == 0 || headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct().Count() != headers.Length)
            throw new AppException(400, "Each CSV column needs a unique, non-empty header.");
    }

    public static ParsedCsv Parse(string text, Guid accountId, CsvMapping mapping, string source = "Csv")
    {
        var transactions = new List<Transaction>();
        var errors = new List<ImportError>();
        var occurrences = new Dictionary<string, int>();
        using var csv = Reader(text);
        if (!csv.Read())
            return new(transactions, [new(1, "The CSV is empty.")]);
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];
        CheckHeaders(headers);
        bool single = !string.IsNullOrWhiteSpace(mapping.AmountColumn);
        if (!single && (string.IsNullOrWhiteSpace(mapping.DebitColumn) || string.IsNullOrWhiteSpace(mapping.CreditColumn)))
            return new(transactions, [new(1, "Choose an amount column or both debit and credit columns.")]);
        var required = new[] { mapping.DateColumn, mapping.DescriptionColumn, single ? mapping.AmountColumn : mapping.DebitColumn, single ? null : mapping.CreditColumn };
        if (required.Where(c => c != null).Any(c => !headers.Contains(c)))
            return new(transactions, [new(1, "A mapped column was not found.")]);
        try
        {
            while (csv.Read())
            {
                int row = csv.Parser.Row;
                try
                {
                    if (!DateOnly.TryParseExact(csv.GetField(mapping.DateColumn), mapping.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date == default)
                        throw new FormatException("Date doesn't match the selected format.");
                    string description = csv.GetField(mapping.DescriptionColumn)?.Trim() ?? "";
                    if (description.Length == 0 || description.Length > 500)
                        throw new FormatException("Description must contain 1–500 characters.");
                    decimal amount;
                    if (single)
                        amount = ParseAmount(csv.GetField(mapping.AmountColumn!), false);
                    else
                    {
                        decimal debit = ParseAmount(csv.GetField(mapping.DebitColumn!), true);
                        decimal credit = ParseAmount(csv.GetField(mapping.CreditColumn!), true);
                        if (debit != 0 && credit != 0)
                            throw new FormatException("A row cannot contain both a debit and a credit.");
                        amount = Math.Abs(credit) - Math.Abs(debit);
                    }
                    if (mapping.InvertSign)
                        amount = -amount;
                    if (amount == 0 || Math.Abs(amount) > 9999999999999999.99m || decimal.Round(amount, 2) != amount)
                        throw new FormatException("Amount must be non-zero with at most two decimal places.");
                    string key = ImportIdentity.Create(accountId, date, amount, description, 0);
                    int occurrence = occurrences.GetValueOrDefault(key);
                    occurrences[key] = occurrence + 1;
                    transactions.Add(new Transaction { AccountId = accountId, Date = date, Description = description, Amount = amount, Source = source, ImportHash = ImportIdentity.Create(accountId, date, amount, description, occurrence) });
                }
                catch (Exception ex) when (ex is FormatException or CsvHelperException or OverflowException)
                {
                    errors.Add(new(row, ex is CsvHelperException ? "The row has missing or invalid fields." : ex.Message));
                }
            }
        }
        catch (CsvHelperException) { errors.Add(new(csv.Parser.Row, "Invalid CSV format.")); }
        return new(transactions, errors);
    }

    private static decimal ParseAmount(string? value, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(value) && allowEmpty)
            return 0;
        if (!decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowParentheses | NumberStyles.AllowCurrencySymbol, CultureInfo.GetCultureInfo("en-US"), out var amount))
            throw new FormatException("Amount is not a valid number.");
        return amount;
    }

    public async Task<ImportResult> Commit(string userId, Guid accountId, string text, CsvMapping mapping, string source = "Csv")
    {
        if (!await db.Accounts.AnyAsync(a => a.UserId == userId && a.Id == accountId))
            throw new AppException(404, "Account not found.");
        var parsed = Parse(text, accountId, mapping, source);
        if (parsed.Errors.Count > 0)
            return new(0, 0, parsed.Errors);
        await using var transaction = await db.Database.BeginTransactionAsync();
        // I lock this user's imports so two uploads cannot save the same rows at once.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({userId}))");
        var saved = await db.Transactions.Where(t => t.UserId == userId && t.ImportHash != null).Select(t => t.ImportHash!).ToListAsync();
        var hashes = saved.ToHashSet();
        var rules = await db.Rules.Where(r => r.UserId == userId).ToListAsync();
        int imported = 0;
        foreach (var item in parsed.Transactions)
        {
            if (!hashes.Add(item.ImportHash!))
                continue;
            item.UserId = userId;
            item.CategoryId = CategoryMatcher.Match(item.Description, rules);
            db.Transactions.Add(item);
            imported++;
        }
        await db.SaveChangesAsync();
        await finance.DetectSubscriptions(userId);
        await transaction.CommitAsync();
        return new(imported, parsed.Transactions.Count - imported, []);
    }
}
