using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Api.Controllers;

[Route("api/categories")]
public class CategoriesController(AppDbContext db) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.Categories.Where(c => c.UserId == UserId).OrderBy(c => c.Name).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(CategoryRequest request)
    {
        var item = new Category { UserId = UserId, Name = request.Name.Trim(), Kind = request.Kind };
        db.Categories.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CategoryRequest request)
    {
        var item = await db.Categories.FirstOrDefaultAsync(c => c.UserId == UserId && c.Id == id);
        if (item == null)
            return NotFound();
        if (item.Kind != request.Kind && await db.Budgets.AnyAsync(b => b.UserId == UserId && b.CategoryId == id))
            return Problem(statusCode: 409, title: "Remove this category's budgets before changing its kind.");
        item.Name = request.Name.Trim();
        item.Kind = request.Kind;
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, Guid? replacementId)
    {
        var item = await db.Categories.FirstOrDefaultAsync(c => c.UserId == UserId && c.Id == id);
        if (item == null)
            return NotFound();
        var transactions = await db.Transactions.Where(t => t.UserId == UserId && t.CategoryId == id).ToListAsync();
        var rules = await db.Rules.Where(r => r.UserId == UserId && r.CategoryId == id).ToListAsync();
        var budgets = await db.Budgets.Where(b => b.UserId == UserId && b.CategoryId == id).ToListAsync();
        if (transactions.Count + rules.Count + budgets.Count > 0)
        {
            var replacement = await db.Categories.FirstOrDefaultAsync(c => c.UserId == UserId && c.Id == replacementId && c.Id != id);
            if (replacement == null)
                return Problem(statusCode: 409, title: "Choose a replacement category before deleting this category.");
            if (replacement.Kind != item.Kind)
                return Problem(statusCode: 409, title: "Choose a replacement with the same category kind.");
            foreach (var t in transactions)
                t.CategoryId = replacement.Id;
            foreach (var r in rules)
                r.CategoryId = replacement.Id;
            var existing = await db.Budgets.Where(b => b.UserId == UserId && b.CategoryId == replacement.Id).ToListAsync();
            foreach (var budget in budgets)
            {
                var match = existing.FirstOrDefault(b => b.Month == budget.Month);
                if (match != null)
                {
                    match.Limit += budget.Limit;
                    db.Budgets.Remove(budget);
                }
                else
                    budget.CategoryId = replacement.Id;
            }
        }
        db.Categories.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
