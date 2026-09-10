using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PocketLense.Api.Contracts;
using PocketLense.Core.Models;
using PocketLense.Infrastructure.Data;

namespace PocketLense.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(UserManager<IdentityUser> users, AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(LoginRequest request)
    {
        var user = new IdentityUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code.Contains("Password") ? "password" : "email", error.Description);
            return ValidationProblem(ModelState);
        }
        string[] names = ["Housing", "Utilities", "Groceries", "Dining", "Transport", "Shopping", "Entertainment", "Subscriptions", "Health", "Other"];
        db.Categories.AddRange(names.Select(name => new Category { UserId = user.Id, Name = name }));
        db.Categories.Add(new Category { UserId = user.Id, Name = "Income", Kind = "Income" });
        db.Categories.Add(new Category { UserId = user.Id, Name = "Transfers", Kind = "Transfer" });
        db.Accounts.Add(new Account { UserId = user.Id, Name = "Checking" });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(Token(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user == null || await users.IsLockedOutAsync(user))
            return Problem(statusCode: 401, title: "Email or password is incorrect.");
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return Problem(statusCode: 401, title: "Email or password is incorrect.");
        }
        await users.ResetAccessFailedCountAsync(user);
        return Ok(Token(user));
    }

    private object Token(IdentityUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(60);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"]!));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"],
            [new Claim(ClaimTypes.NameIdentifier, user.Id)], expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt = expires,
            email = user.Email
        };
    }
}
