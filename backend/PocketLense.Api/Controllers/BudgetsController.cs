using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Infrastructure.Data;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[Route("api/budgets")]
public class BudgetsController(AppDbContext db, FinanceService finance) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get(string? month) => Ok(await finance.BudgetStatus(UserId, Month(month)));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.UserId == UserId && b.Id == id);
        return budget == null ? NotFound() : Ok(budget);
    }

    [HttpPut]
    public async Task<IActionResult> Save(BudgetRequest request)
    {
        if (request.Month == default || request.Month.Day != 1 || decimal.Round(request.Limit, 2) != request.Limit)
            return Problem(statusCode: 400, title: "Use the first day of a month and a limit with at most two decimal places.");
        await CheckCategory(db, request.CategoryId);
        if (!await db.Categories.AnyAsync(c => c.UserId == UserId && c.Id == request.CategoryId && c.Kind == "Expense"))
            return Problem(statusCode: 400, title: "Budgets need an expense category.");
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.UserId == UserId && b.CategoryId == request.CategoryId && b.Month == request.Month);
        if (budget == null)
        {
            budget = new Budget { UserId = UserId, CategoryId = request.CategoryId, Month = request.Month };
            db.Budgets.Add(budget);
        }
        budget.Limit = request.Limit;
        await db.SaveChangesAsync();
        return Ok(budget);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var budget = await db.Budgets.FirstOrDefaultAsync(b => b.UserId == UserId && b.Id == id);
        if (budget == null)
            return NotFound();
        db.Budgets.Remove(budget);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("copy-previous")]
    public async Task<IActionResult> Copy(string? month)
    {
        var target = Month(month);
        var previous = target.AddMonths(-1);
        var budgets = await db.Budgets.AsNoTracking().Where(b => b.UserId == UserId && b.Month == previous).ToListAsync();
        var existing = await db.Budgets.Where(b => b.UserId == UserId && b.Month == target).Select(b => b.CategoryId).ToListAsync();
        int copied = 0;
        foreach (var budget in budgets.Where(b => !existing.Contains(b.CategoryId)))
        {
            db.Budgets.Add(new Budget { UserId = UserId, CategoryId = budget.CategoryId, Month = target, Limit = budget.Limit });
            copied++;
        }
        await db.SaveChangesAsync();
        return Ok(new
        {
            copied
        });
    }
}
