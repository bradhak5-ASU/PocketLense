using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Core.Services;
using PocketLense.Infrastructure.Data;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[Route("api/transactions")]
public class TransactionsController(AppDbContext db) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get(string? month, Guid? accountId, Guid? categoryId, string? search, int page = 1, int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Transactions.AsNoTracking().Where(t => t.UserId == UserId);
        if (month != null)
        {
            var start = Month(month);
            var end = start.AddMonths(1);
            query = query.Where(t => t.Date >= start && t.Date < end);
        }
        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId);
        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Description.ToLower().Contains(search.ToLower()));
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.Date).ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new
        {
            items,
            total,
            page,
            pageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var item = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == UserId && t.Id == id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(TransactionRequest request)
    {
        var item = new Transaction { UserId = UserId };
        await SetValues(item, request);
        var source = new ManualTransactionSource(item);
        db.Transactions.AddRange(await source.ReadAsync());
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, TransactionRequest request)
    {
        var item = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == UserId && t.Id == id);
        if (item == null)
            return NotFound();
        await SetValues(item, request);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    private async Task SetValues(Transaction item, TransactionRequest request)
    {
        if (!await db.Accounts.AnyAsync(a => a.UserId == UserId && a.Id == request.AccountId))
            throw new AppException(404, "Account not found.");
        await CheckCategory(db, request.CategoryId);
        item.Date = request.Date;
        item.Description = request.Description.Trim();
        item.Amount = request.Amount;
        item.AccountId = request.AccountId;
        item.CategoryId = request.CategoryId ?? CategoryMatcher.Match(item.Description, await db.Rules.Where(r => r.UserId == UserId).ToListAsync());
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var item = await db.Transactions.FirstOrDefaultAsync(t => t.UserId == UserId && t.Id == id);
        if (item == null)
            return NotFound();
        db.Transactions.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
