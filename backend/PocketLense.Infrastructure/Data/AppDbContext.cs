using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PocketLense.Core.Models;

namespace PocketLense.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CategoryRule> Rules => Set<CategoryRule>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Account>().Property(a => a.Name).HasMaxLength(100);
        builder.Entity<Category>().Property(c => c.Name).HasMaxLength(100);
        builder.Entity<CategoryRule>().Property(r => r.Keyword).HasMaxLength(200);
        builder.Entity<Transaction>().Property(t => t.Description).HasMaxLength(500);
        builder.Entity<Transaction>().Property(t => t.Amount).HasPrecision(18, 2);
        builder.Entity<Budget>().Property(b => b.Limit).HasPrecision(18, 2);
        builder.Entity<Subscription>().Property(s => s.Amount).HasPrecision(18, 2);
        builder.Entity<Subscription>().Property(s => s.PreviousAmount).HasPrecision(18, 2);
        builder.Entity<Transaction>().HasIndex(t => new { t.UserId, t.ImportHash }).IsUnique().HasFilter("\"ImportHash\" IS NOT NULL");
        builder.Entity<Transaction>().HasIndex(t => new { t.UserId, t.Date });
        builder.Entity<Budget>().HasIndex(b => new { b.UserId, b.CategoryId, b.Month }).IsUnique();
        builder.Entity<Subscription>().HasIndex(s => new { s.UserId, s.MerchantKey }).IsUnique().HasFilter("NOT \"IsManual\"");
        builder.Entity<Account>().HasAlternateKey(a => new { a.UserId, a.Id });
        builder.Entity<Category>().HasAlternateKey(c => new { c.UserId, c.Id });
        builder.Entity<Transaction>().HasOne<Account>().WithMany().HasForeignKey(t => new { t.UserId, t.AccountId }).HasPrincipalKey(a => new { a.UserId, a.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Transaction>().HasOne<Category>().WithMany().HasForeignKey(t => new { t.UserId, t.CategoryId }).HasPrincipalKey(c => new { c.UserId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Budget>().HasOne<Category>().WithMany().HasForeignKey(b => new { b.UserId, b.CategoryId }).HasPrincipalKey(c => new { c.UserId, c.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<CategoryRule>().HasOne<Category>().WithMany().HasForeignKey(r => new { r.UserId, r.CategoryId }).HasPrincipalKey(c => new { c.UserId, c.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
