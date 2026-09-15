using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Core.Services;
using PocketLense.Infrastructure.Data;
using PocketLense.Infrastructure.Services;

namespace PocketLense.Api.Controllers;

[Route("api/subscriptions")]
public class SubscriptionsController(AppDbContext db, FinanceService finance) : UserController
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var items = await db.Subscriptions.Where(s => s.UserId == UserId).OrderBy(s => s.NextDate).ToListAsync();
        decimal monthly = items.Where(s => s.Status == "Confirmed").Sum(SubscriptionDetector.MonthlyCost);
        return Ok(new
        {
            items,
            monthlyCost = Math.Round(monthly, 2),
            annualCost = Math.Round(monthly * 12, 2),
            upcoming = await finance.UpcomingBills(UserId, 30)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOne(Guid id)
    {
        var item = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == UserId && s.Id == id);
        return item == null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SubscriptionRequest request)
    {
        if (request.NextDate == default || request.NextDate.Year > 9990 || decimal.Round(request.Amount, 2) != request.Amount)
            return Problem(statusCode: 400, title: "Enter a renewal date and an amount with at most two decimal places.");
        var item = new Subscription { UserId = UserId, DisplayName = request.DisplayName.Trim(), MerchantKey = SubscriptionDetector.MerchantKey(request.DisplayName), Amount = request.Amount, Frequency = request.Frequency, NextDate = request.NextDate, IsManual = true, Status = "Confirmed" };
        db.Subscriptions.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<IActionResult> Confirm(Guid id) => SetStatus(id, "Confirmed");
    [HttpPost("{id:guid}/dismiss")]
    public Task<IActionResult> Dismiss(Guid id) => SetStatus(id, "Dismissed");

    private async Task<IActionResult> SetStatus(Guid id, string status)
    {
        var item = await db.Subscriptions.FirstOrDefaultAsync(s => s.UserId == UserId && s.Id == id);
        if (item == null)
            return NotFound();
        item.Status = status;
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpPost("detect")]
    public async Task<IActionResult> Detect()
    {
        await finance.DetectSubscriptions(UserId);
        return Ok(new
        {
            message = "Subscription detection complete."
        });
    }
}
