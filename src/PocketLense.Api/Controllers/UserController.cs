using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Infrastructure.Data;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[ApiController, Authorize]
public abstract class UserController : ControllerBase
{
    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    protected static DateOnly Month(string? value)
    {
        if (value == null)
            return new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        if (!DateOnly.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            || month.Year < 1901 || month.Year > 9998)
            throw new AppException(400, "Choose a valid month (YYYY-MM).");
        return month;
    }

    protected async Task CheckCategory(AppDbContext db, Guid? categoryId)
    {
        if (categoryId.HasValue && !await db.Categories.AnyAsync(c => c.UserId == UserId && c.Id == categoryId))
            throw new AppException(404, "Category not found.");
    }
}
