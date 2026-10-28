using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Data;

/// <summary>
/// Учётка персонала из конфигурации.
/// </summary>
public class StaffAccount
{
    public string Login { get; set; } = "";

    public string Password { get; set; } = "";
}

/// <summary>
/// Учётки админа и ТВ.
/// </summary>
public class AccountsOptions
{
    public const string Section = "Accounts";

    public StaffAccount Admin { get; set; } = new();

    public StaffAccount Viewer { get; set; } = new();
}

/// <summary>
/// Создаёт базу и учётки персонала при старте.
/// </summary>
public class StaffSeeder(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    IOptions<AccountsOptions> accounts,
    TimeProvider time)
{
    /// <summary>
    /// Создаёт схему (если нет) и учётки Admin/Viewer. Пароль из конфига перезаписывает хэш при каждом старте.
    /// </summary>
    public async Task SeedAsync()
    {
        await db.Database.EnsureCreatedAsync();
        await UpsertAsync(accounts.Value.Admin, UserRole.Admin);
        await UpsertAsync(accounts.Value.Viewer, UserRole.Viewer);
        await db.SaveChangesAsync();
    }

    private async Task UpsertAsync(StaffAccount account, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(account.Login) || string.IsNullOrEmpty(account.Password))
        {
            throw new InvalidOperationException($"Не задана учётка {role} в секции {AccountsOptions.Section}.");
        }

        var normalized = User.Normalize(account.Login);
        var user = await db.Users.SingleOrDefaultAsync(u => u.LoginNormalized == normalized);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Login = account.Login.Trim(),
                LoginNormalized = normalized,
                PasswordHash = "",
                Role = role,
                CreatedAt = time.GetUtcNow(),
            };
            db.Users.Add(user);
        }

        user.Role = role;
        user.PasswordHash = hasher.HashPassword(user, account.Password);
    }
}
