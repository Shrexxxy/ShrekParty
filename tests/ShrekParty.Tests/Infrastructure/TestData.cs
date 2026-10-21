using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Tests.Infrastructure;

/// <summary>
/// Прямое наполнение базы для тестов.
/// </summary>
public static class TestData
{
    /// <summary>Пароль игроков, созданных через <see cref="SeedPlayerAsync"/>.</summary>
    public const string PlayerPassword = "player-test-pass";

    /// <summary>Создаёт игрока сразу в базе.</summary>
    public static async Task<Guid> SeedPlayerAsync(this AppFactory factory, string login)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Login = login,
            LoginNormalized = User.Normalize(login),
            PasswordHash = "",
            Role = UserRole.Player,
            CreatedAt = factory.Time.GetUtcNow(),
        };
        user.PasswordHash = hasher.HashPassword(user, PlayerPassword);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    /// <summary>Создаёт завершённый ивент.</summary>
    public static async Task<Guid> SeedEventAsync(this AppFactory factory, string title)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Type = EventType.Registration,
            Title = title,
            Status = EventStatus.Finished,
            MaxDonkeys = 100,
            Scheme = ScoringScheme.Linear,
            StartedAt = factory.Time.GetUtcNow(),
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        return ev.Id;
    }

    /// <summary>Выполняет действие с сервисом из DI в отдельном scope.</summary>
    public static async Task WithServiceAsync<T>(this AppFactory factory, Func<T, Task> action)
        where T : notnull
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<T>());
    }
}
