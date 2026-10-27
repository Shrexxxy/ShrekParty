using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.HideAndSeek;

/// <summary>Фаза пряток.</summary>
public enum HidePhase
{
    /// <summary>Свет выключен, все прячутся.</summary>
    Hiding,

    /// <summary>Именинник фоткает со вспышкой.</summary>
    Shooting,

    /// <summary>Голосование за самое смешное фото.</summary>
    Voting,
}

/// <summary>Состояние пряток в Event.StateJson.</summary>
public record HideState(HidePhase Phase, DateTimeOffset Deadline);

/// <summary>Игрок и сколько раз попал на фото — только админу.</summary>
public record HideHitsDto(Guid UserId, string Login, string? AvatarUrl, int Hits);

/// <summary>Снимок: кто на нём и сколько голосов.</summary>
public record HideShotDto(Guid Id, string Url, IReadOnlyList<Guid> UserIds, int Votes);

/// <summary>Детали пряток.</summary>
/// <param name="Players">Только админу.</param>
/// <param name="Shots">Админу всегда, остальным — с голосования.</param>
public record HideDetails(
    HidePhase Phase,
    DateTimeOffset Deadline,
    IReadOnlyList<HideHitsDto> Players,
    IReadOnlyList<HideShotDto> Shots,
    Guid? MyVote);

/// <summary>
/// Фотопрятки в темноте: «Прячьтесь» 2 минуты → съёмка 10 минут → голосование 10 минут.
/// Кто реже попадал на фото — выше; герои самого смешного фото получают бонус.
/// </summary>
public class HideSeekModule(AppDbContext db, TimeProvider time) : IEventModule
{
    /// <summary>Тег медиа снимков пряток — по нему их чистит сброс.</summary>
    public const string MediaTag = "hide";

    /// <summary>Бонус каждому, кто попал на самое смешное фото.</summary>
    public const int FunniestBonus = 15;

    /// <summary>Причина бонуса в истории осликов.</summary>
    public const string FunniestReason = "Самое смешное фото";

    /// <summary>Сколько даётся спрятаться.</summary>
    public static readonly TimeSpan HideTime = TimeSpan.FromMinutes(2);

    /// <summary>Сколько длится съёмка.</summary>
    public static readonly TimeSpan ShootTime = TimeSpan.FromMinutes(10);

    /// <summary>Сколько длится голосование.</summary>
    public static readonly TimeSpan VoteTime = TimeSpan.FromMinutes(10);

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.HideAndSeek;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        await db.EnsurePlayersAsync();

        SetState(ev, new HideState(HidePhase.Hiding, time.GetUtcNow() + HideTime));
    }

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var state = StateOf(ev);
        var showShots = isAdmin || state.Phase == HidePhase.Voting;
        if (!showShots)
        {
            // Пока темно, телефону игрока нечего показывать — только фаза и таймер.
            return new HideDetails(state.Phase, state.Deadline, [], [], null);
        }

        var shots = await ShotsAsync(ev.Id);
        var votes = await db.HideVotes.AsNoTracking().Where(v => v.EventId == ev.Id).ToListAsync();
        var shotDtos = shots
            .Select(s => new HideShotDto(s.Shot.Id, MediaStorage.UrlFor(s.Shot.MediaId), s.UserIds, votes.Count(v => v.ShotId == s.Shot.Id)))
            .ToList();

        var players = isAdmin
            ? (await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync())
                .Select(u => new HideHitsDto(u.Id, u.Login, u.ToMe().AvatarUrl, shots.Count(s => s.UserIds.Contains(u.Id))))
                .OrderBy(p => p.Login, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        return new HideDetails(state.Phase, state.Deadline, players, shotDtos, votes.FirstOrDefault(v => v.VoterId == viewerId)?.ShotId);
    }

    /// <inheritdoc/>
    public async Task<bool> TickAsync(Event ev, DateTimeOffset now)
    {
        var state = StateOf(ev);
        if (now < state.Deadline)
        {
            return false;
        }

        switch (state.Phase)
        {
            case HidePhase.Hiding:
                SetState(ev, new HideState(HidePhase.Shooting, now + ShootTime));
                return false;
            case HidePhase.Shooting:
                // Голосовать не за что — сразу итоги.
                if (!await HasShotsAsync(db, ev.Id))
                {
                    return true;
                }

                StartVoting(ev, now);
                return false;
            default:
                return true;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        // Зарегистрировался позже — не прятался; иначе 0 попаданий дали бы ему 1-е место.
        var players = await db.Users.Where(u => u.Role == UserRole.Player && u.CreatedAt <= ev.StartedAt).Select(u => u.Id).ToListAsync();
        var hits = await db.HideShotPlayers
            .Where(p => db.HideShots.Any(s => s.Id == p.ShotId && s.EventId == ev.Id))
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);

        return DenseRanking.Rank(players, p => hits.GetValueOrDefault(p), descending: false)
            .Select(r => new Ranked<Guid>(r.Item, r.Place))
            .ToList();
    }

    /// <inheritdoc/>
    public async Task AfterFinishAsync(Event ev)
    {
        var votes = await db.HideVotes.Where(v => v.EventId == ev.Id)
            .GroupBy(v => v.ShotId)
            .Select(g => new { ShotId = g.Key, Count = g.Count() })
            .ToListAsync();
        if (votes.Count == 0)
        {
            return;
        }

        var max = votes.Max(v => v.Count);
        var winners = votes.Where(v => v.Count == max).Select(v => v.ShotId).ToList();
        var heroes = await db.HideShotPlayers.Where(p => winners.Contains(p.ShotId)).Select(p => p.UserId).Distinct().ToListAsync();
        db.DonkeyAwards.AddRange(heroes.Select(userId => new DonkeyAward
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventId = ev.Id,
            Amount = FunniestBonus,
            Reason = FunniestReason,
            CreatedAt = ev.FinishedAt ?? DateTimeOffset.UtcNow,
        }));
    }

    internal static HideState StateOf(Event ev) => JsonSerializer.Deserialize<HideState>(ev.StateJson!, Json)!;

    internal static Task<bool> HasShotsAsync(AppDbContext db, Guid eventId) => db.HideShots.AnyAsync(s => s.EventId == eventId);

    internal static void StartVoting(Event ev, DateTimeOffset now) => SetState(ev, new HideState(HidePhase.Voting, now + VoteTime));

    private static void SetState(Event ev, HideState state) => ev.StateJson = JsonSerializer.Serialize(state, Json);

    private async Task<List<(HideShot Shot, List<Guid> UserIds)>> ShotsAsync(Guid eventId)
    {
        var shots = await db.HideShots.AsNoTracking().Where(s => s.EventId == eventId).OrderBy(s => s.TakenAt).ToListAsync();
        var ids = shots.Select(s => s.Id).ToList();
        var marks = await db.HideShotPlayers.AsNoTracking().Where(p => ids.Contains(p.ShotId)).ToListAsync();
        return shots.Select(s => (s, marks.Where(m => m.ShotId == s.Id).Select(m => m.UserId).ToList())).ToList();
    }
}
