using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Reaction;

/// <summary>Фаза раунда реакции.</summary>
public enum ReactionPhase
{
    /// <summary>Экраны красные, зелёный — в случайный момент.</summary>
    Red,

    /// <summary>Зелёный — тапай!</summary>
    Green,

    /// <summary>Табличка раунда перед следующим.</summary>
    Result,
}

/// <summary>Состояние реакции в Event.StateJson; в фазе Red дедлайн — скрытый момент зелёного.</summary>
public record ReactionState(int Round, ReactionPhase Phase, DateTimeOffset Deadline);

/// <summary>Попытка в таблице: время или фальстарт.</summary>
public record ReactionAttemptDto(int Round, int? Ms, bool FalseStart);

/// <summary>Строка таблицы: все попытки и лучшая.</summary>
public record ReactionRowDto(Guid UserId, string Login, string? AvatarUrl, IReadOnlyList<ReactionAttemptDto> Attempts, int? Best);

/// <summary>Детали реакции.</summary>
/// <param name="Deadline">Конец фазы; в Red — <c>null</c>, момент зелёного не раскрываем.</param>
/// <param name="MyAttempt">Время игрока в текущем раунде.</param>
/// <param name="MyFalseStart">Игрок фальстартовал в текущем раунде.</param>
public record ReactionDetails(
    int Round,
    int TotalRounds,
    ReactionPhase Phase,
    DateTimeOffset? Deadline,
    IReadOnlyList<ReactionRowDto> Rows,
    int? MyAttempt,
    bool MyFalseStart);

/// <summary>
/// Реакция Шрека: 3 общих раунда «красный → зелёный через 2–6 с → тап», в зачёт лучшее время.
/// Раунды идут сами: тик переключает фазы, после третьего — итог.
/// </summary>
public class ReactionModule(AppDbContext db, Random random) : IEventModule
{
    /// <summary>Сколько раундов.</summary>
    public const int Rounds = 3;

    private const int MinRedMs = 2000;
    private const int MaxRedMs = 6000;

    /// <summary>Сколько горит зелёный.</summary>
    public static readonly TimeSpan GreenTime = TimeSpan.FromSeconds(5);

    /// <summary>Пауза с табличкой между раундами.</summary>
    public static readonly TimeSpan ResultTime = TimeSpan.FromSeconds(4);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Reaction;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        await db.EnsurePlayersAsync();

        SetState(ev, RedRound(1, ev.StartedAt!.Value));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var attempts = await db.ReactionAttempts.AsNoTracking().Where(a => a.EventId == ev.Id).ToListAsync();
        var players = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync();
        var rows = players
            .Select(p =>
            {
                var mine = attempts.Where(a => a.UserId == p.Id).OrderBy(a => a.Round).ToList();
                return new ReactionRowDto(
                    p.Id,
                    p.Login,
                    p.ToMe().AvatarUrl,
                    mine.Select(a => new ReactionAttemptDto(a.Round, a.Ms, a.Ms is null)).ToList(),
                    mine.Min(a => a.Ms));
            })
            .OrderBy(r => r.Best is null)
            .ThenBy(r => r.Best)
            .ThenBy(r => r.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var current = attempts.FirstOrDefault(a => a.UserId == viewerId && a.Round == state.Round);

        return new ReactionDetails(
            state.Round,
            Rounds,
            state.Phase,
            state.Phase == ReactionPhase.Red ? null : state.Deadline,
            rows,
            current?.Ms,
            current is { Ms: null });
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (now < state.Deadline)
        {
            return Task.FromResult(false);
        }

        switch (state.Phase)
        {
            case ReactionPhase.Red:
                SetState(ev, state with { Phase = ReactionPhase.Green, Deadline = now + GreenTime });
                return Task.FromResult(false);
            case ReactionPhase.Green:
                SetState(ev, state with { Phase = ReactionPhase.Result, Deadline = now + ResultTime });
                return Task.FromResult(false);
            default:
                if (state.Round >= Rounds)
                {
                    return Task.FromResult(true);
                }

                SetState(ev, RedRound(state.Round + 1, now));
                return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var best = await db.ReactionAttempts.Where(a => a.EventId == ev.Id && a.Ms != null)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Best = g.Min(a => a.Ms!.Value) })
            .ToListAsync();
        return DenseRanking.Rank(best, b => b.Best, descending: false)
            .Select(r => new Ranked<Guid>(r.Item.UserId, r.Place))
            .ToList();
    }

    internal static ReactionState StateOf(Event ev) => JsonSerializer.Deserialize<ReactionState>(ev.StateJson!, Json)!;

    private ReactionState RedRound(int round, DateTimeOffset now) =>
        new(round, ReactionPhase.Red, now + TimeSpan.FromMilliseconds(random.Next(MinRedMs, MaxRedMs + 1)));

    private static void SetState(Event ev, ReactionState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);
}
