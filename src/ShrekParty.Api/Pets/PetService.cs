using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Pets;

/// <summary>
/// Драконоослики: появляются после закрытия регистрации (опоздавшим — сразу), вылупляются, растут от заработанных осликов.
/// </summary>
public class PetService(AppDbContext db, ILiveNotifier live, Random random, TimeProvider time)
{
    /// <summary>Длина клички.</summary>
    public const int MaxNameLength = 20;

    /// <summary>Закрытие регистрации и опоздавшие могут создавать яйца разом — по очереди, чтобы не завести питомца дважды.</summary>
    private static readonly SemaphoreSlim EnsureLock = new(1, 1);

    /// <summary>
    /// Яйцо каждому игроку, у кого его ещё нет. Классы — по одному, всегда тот, которого меньше, — компания делится поровну.
    /// </summary>
    public async Task EnsureAsync()
    {
        await EnsureLock.WaitAsync();
        try
        {
            await CreateMissingAsync();
        }
        finally
        {
            EnsureLock.Release();
        }
    }

    /// <summary>Яйцо натапано — питомец вылупился (повтор ничего не меняет).</summary>
    public async Task HatchAsync(Guid userId)
    {
        var pet = await db.Pets.FindAsync(userId) ?? throw new KeyNotFoundException();
        if (pet.HatchedAt is not null)
        {
            return;
        }

        pet.HatchedAt = time.GetUtcNow();
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>Вылупить все оставшиеся яйца — перед боем с боссом в бой идут все.</summary>
    public async Task HatchAllAsync()
    {
        var now = time.GetUtcNow();
        await db.Pets.Where(p => p.HatchedAt == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.HatchedAt, now));
    }

    /// <summary>Новая кличка: без пробелов по краям, 1–20 символов.</summary>
    public async Task RenameAsync(Guid userId, string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length is 0 or > MaxNameLength)
        {
            throw new BadInputException("bad_pet_name");
        }

        var pet = await db.Pets.FindAsync(userId) ?? throw new KeyNotFoundException();
        pet.Name = trimmed;
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>Ни один ивент программы (кроме регистрации) ещё не стартовал.</summary>
    public async Task<bool> BeforeFirstEventAsync() =>
        !await db.Events.AnyAsync(e => e.Type != EventType.Registration && e.StartedAt != null);

    /// <summary>Все питомцы с уровнем и характеристиками по заработанному на сейчас.</summary>
    public async Task<IReadOnlyList<PetDto>> AllAsync()
    {
        var pets = await db.Pets.AsNoTracking().Join(db.Users, p => p.UserId, u => u.Id, (p, u) => new { Pet = p, u.Login }).ToListAsync();

        // Опыт — только заработанное: штрафы, ставки и стартовый капитал уровень не меняют.
        var earned = await db.DonkeyAwards
            .Where(a => a.Amount > 0 && a.Reason != DonkeyReasons.StartCapital)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.UserId, x => x.Sum);

        return pets.Select(x => ToDto(x.Pet, x.Login, earned.GetValueOrDefault(x.Pet.UserId))).ToList();
    }

    private async Task CreateMissingAsync()
    {
        var players = await db.Users.Where(u => u.Role == UserRole.Player && !db.Pets.Any(p => p.UserId == u.Id)).ToListAsync();
        if (players.Count == 0)
        {
            return;
        }

        var counts = await db.Pets.GroupBy(p => p.Class).ToDictionaryAsync(g => g.Key, g => g.Count());
        var now = time.GetUtcNow();
        foreach (var user in players.OrderBy(_ => random.Next()))
        {
            var petClass = PetRules.PickClass(counts, random);
            counts[petClass] = counts.GetValueOrDefault(petClass) + 1;
            var start = PetRules.Roll(petClass, random);
            db.Pets.Add(new Pet
            {
                UserId = user.Id,
                Name = $"Дракоша {user.Login}",
                Class = petClass,
                Strength = start.Stats.Strength,
                Agility = start.Stats.Agility,
                Intellect = start.Stats.Intellect,
                FirstMinor = start.FirstMinor,
                CreatedAt = now,
            });
        }

        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    private static PetDto ToDto(Pet pet, string login, int earned)
    {
        var level = PetRules.Level(earned);
        var stats = PetRules.StatsAt(new PetStart(pet.Class, new PetStats(pet.Strength, pet.Agility, pet.Intellect), pet.FirstMinor), level);
        return new PetDto(
            pet.UserId, login, pet.Name, pet.Class, pet.HatchedAt is not null, level, earned, PetRules.NextLevelAt(level),
            stats.Strength, stats.Agility, stats.Intellect, stats.Health, stats.Damage, stats.Mana);
    }
}

/// <summary>
/// Маршруты питомцев.
/// </summary>
public static class PetEndpoints
{
    /// <summary>
    /// <c>/api/pets/hatch|name</c> (игрок), <c>/api/admin/pets/{userId}/name</c> (ведущий).
    /// </summary>
    public static IEndpointRouteBuilder MapPets(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var player = routes.MapGroup("/api/pets").RequireAuthorization(AuthEndpoints.PlayerPolicy);

        player.MapPost("/hatch", async (ClaimsPrincipal user, PetService pets) =>
        {
            await pets.HatchAsync(user.UserId()!.Value);
            return Results.NoContent();
        });

        player.MapPut("/name", async (PetNameRequest body, ClaimsPrincipal user, PetService pets) =>
        {
            await pets.RenameAsync(user.UserId()!.Value, body.Name);
            return Results.NoContent();
        });

        routes.MapPut("/api/admin/pets/{userId:guid}/name", async (Guid userId, PetNameRequest body, PetService pets) =>
            {
                await pets.RenameAsync(userId, body.Name);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }
}
