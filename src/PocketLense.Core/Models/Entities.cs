namespace PocketLense.Core.Models;

public class UserRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = "";
}

public class Account : UserRecord
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "Checking";
}

public class Category : UserRecord
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "Expense";
}

public class CategoryRule : UserRecord
{
    public string Keyword { get; set; } = "";
    public Guid CategoryId
    { get; set; }
}

public class Transaction : UserRecord
{
    public Guid AccountId
    { get; set; }
    public DateOnly Date
    { get; set; }
    public string Description { get; set; } = "";
    public decimal Amount
    { get; set; }
    public Guid? CategoryId
    { get; set; }
    public string Source { get; set; } = "Manual";
    public string? ImportHash
    { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Budget : UserRecord
{
    public Guid CategoryId
    { get; set; }
    public DateOnly Month
    { get; set; }
    public decimal Limit
    { get; set; }
}

public class Subscription : UserRecord
{
    public string MerchantKey { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public decimal Amount
    { get; set; }
    public decimal? PreviousAmount
    { get; set; }
    public string Frequency { get; set; } = "Monthly";
    public DateOnly NextDate
    { get; set; }
    public string Status { get; set; } = "Detected";
    public bool IsManual
    { get; set; }
}
