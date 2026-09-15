using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Core.Services;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Api.Controllers;

[Route("api/rules")]
public class RulesController(AppDbContext db) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.Rules.Where(r => r.UserId == UserId).OrderBy(r => r.Keyword).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(RuleRequest request)
    {
        await CheckCategory(db, request.CategoryId);
        var rule = new CategoryRule { UserId = UserId, Keyword = request.Keyword.Trim(), CategoryId = request.CategoryId };
        db.Rules.Add(rule);
        await db.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var rule = await db.Rules.FirstOrDefaultAsync(r => r.UserId == UserId && r.Id == id);
        if (rule == null)
            return NotFound();
        db.Rules.Remove(rule);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("apply")]
    public async Task<IActionResult> Apply()
    {
        var rules = await db.Rules.Where(r => r.UserId == UserId).ToListAsync();
        var transactions = await db.Transactions.Where(t => t.UserId == UserId && t.CategoryId == null).ToListAsync();
        int updated = 0;
        foreach (var transaction in transactions)
        {
            transaction.CategoryId = CategoryMatcher.Match(transaction.Description, rules);
            if (transaction.CategoryId.HasValue)
                updated++;
        }
        await db.SaveChangesAsync();
        return Ok(new
        {
            updated
        });
    }
}
