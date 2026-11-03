using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Pets;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Tapper;

/// <summary>Фаза тапалки.</summary>
public enum TapperPhase
{
    /// <summary>3… 2… 1…</summary>
    Countdown,

    /// <summary>Тапают.</summary>
    Running,
}

/// <summary>Состояние тапалки в Event.StateJson.</summary>
public record TapperState(TapperPhase Phase, DateTimeOffset Deadline);

/// <summary>Дракон игрока в гонке: его питомец (класс и рисунок; null — питомца нет) и сколько натапал.</summary>
public record TapperRowDto(Guid UserId, string Login, string? AvatarUrl, int Taps, PetClass? PetClass, int? PetVariant);

/// <summary>Детали тапалки: все драконы по убыванию тапов.</summary>
public record TapperDetails(TapperPhase Phase, DateTimeOffset Deadline, IReadOnlyList<TapperRowDto> Rows);

/// <summary>
/// Тапалка с драконом: отсчёт 3 с → минута тапов → итог по числу тапов.
/// Тапы приходят пачками и не рассылаются — во время забега снапшот уходит каждый тик.
/// </summary>
public class TapperModule(AppDbContext db, TimeProvider time) : IEventModule
{
    /// <summary>Отсчёт перед стартом.</summary>
    public static readonly TimeSpan CountdownTime = TimeSpan.FromSeconds(3);

    /// <summary>Сколько длится забег.</summary>
    public static readonly TimeSpan RunTime = TimeSpan.FromSeconds(60);

    /// <summary>Сколько после финиша ещё ждём последнюю пачку тапов из сети.</summary>
    public static readonly TimeSpan LastBatchGrace = TimeSpan.FromSeconds(1);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Tapper;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        await db.EnsurePlayersAsync();

        SetState(ev, new TapperState(TapperPhase.Countdown, time.GetUtcNow() + CountdownTime));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var taps = await db.TapScores.AsNoTracking().Where(t => t.EventId == ev.Id).ToDictionaryAsync(t => t.UserId, t => t.Taps);
        var pets = await db.Pets.AsNoTracking().ToDictionaryAsync(p => p.UserId);
        var rows = (await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync())
            .Select(u => new TapperRowDto(
                u.Id, u.Login, u.ToMe().AvatarUrl, taps.GetValueOrDefault(u.Id), pets.GetValueOrDefault(u.Id)?.Class, pets.GetValueOrDefault(u.Id)?.Variant))
            .OrderByDescending(r => r.Taps)
            .ThenBy(r => r.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new TapperDetails(state.Phase, state.Deadline, rows);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (state.Phase == TapperPhase.Countdown)
        {
            if (now >= state.Deadline)
            {
                SetState(ev, new TapperState(TapperPhase.Running, now + RunTime));
            }

            return Task.FromResult(false);
        }

        return Task.FromResult(now >= state.Deadline + LastBatchGrace);
    }

    /// <inheritdoc/>
    public bool BroadcastEveryTick(Event ev) => StateOf(ev).Phase == TapperPhase.Running;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var scores = await db.TapScores.Where(t => t.EventId == ev.Id && t.Taps > 0).ToListAsync();
        return DenseRanking.Rank(scores, s => s.Taps, descending: true)
            .Select(r => new Ranked<Guid>(r.Item.UserId, r.Place))
            .ToList();
    }

    internal static TapperState StateOf(Event ev) => JsonSerializer.Deserialize<TapperState>(ev.StateJson!, Json)!;

    private static void SetState(Event ev, TapperState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);
}
