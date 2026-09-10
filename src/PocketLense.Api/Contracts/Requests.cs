using System.ComponentModel.DataAnnotations;

namespace PocketLense.Api.Contracts;

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";
    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = "";
}
public class AccountRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";
    [RegularExpression("Checking|Credit|Savings")]
    public string Type { get; set; } = "Checking";
}
public class CategoryRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = "";
    [RegularExpression("Expense|Income|Transfer")]
    public string Kind { get; set; } = "Expense";
}
public class RuleRequest
{
    [Required, MaxLength(200)]
    public string Keyword { get; set; } = "";
    public Guid CategoryId
    { get; set; }
}
public class TransactionRequest : IValidatableObject
{
    public DateOnly Date
    { get; set; }
    [Required, MaxLength(500)]
    public string Description { get; set; } = "";
    public decimal Amount
    { get; set; }
    public Guid AccountId
    { get; set; }
    public Guid? CategoryId
    { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Date == default || Date.Year > 9990)
            yield return new ValidationResult("Enter a valid transaction date.", [nameof(Date)]);
        if (Amount == 0 || Amount > 9999999999999999.99m || Amount < -9999999999999999.99m)
            yield return new ValidationResult("Enter a non-zero amount within the supported range.", [nameof(Amount)]);
        if (decimal.Round(Amount, 2) != Amount)
            yield return new ValidationResult("Use at most two decimal places.", [nameof(Amount)]);
    }
}
public class BudgetRequest
{
    public Guid CategoryId
    { get; set; }
    public DateOnly Month
    { get; set; }
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Limit
    { get; set; }
}
public class SubscriptionRequest
{
    [Required, MaxLength(200)]
    public string DisplayName { get; set; } = "";
    [Range(typeof(decimal), "0.01", "9999999999999999.99")]
    public decimal Amount
    { get; set; }
    [RegularExpression("Weekly|Monthly|Annual")]
    public string Frequency { get; set; } = "Monthly";
    public DateOnly NextDate
    { get; set; }
}
