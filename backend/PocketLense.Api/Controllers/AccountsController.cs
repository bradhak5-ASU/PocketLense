using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Api.Controllers;

[Route("api/accounts")]
public class AccountsController(AppDbContext db) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get() => Ok(await db.Accounts.Where(a => a.UserId == UserId).OrderBy(a => a.Name).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(AccountRequest request)
    {
        var account = new Account { UserId = UserId, Name = request.Name.Trim(), Type = request.Type };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return Ok(account);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, AccountRequest request)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == UserId && a.Id == id);
        if (account == null)
            return NotFound();
        account.Name = request.Name.Trim();
        account.Type = request.Type;
        await db.SaveChangesAsync();
        return Ok(account);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == UserId && a.Id == id);
        if (account == null)
            return NotFound();
        if (await db.Transactions.AnyAsync(t => t.UserId == UserId && t.AccountId == id))
            return Problem(statusCode: 409, title: "Move or delete this account's transactions first.");
        db.Accounts.Remove(account);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
