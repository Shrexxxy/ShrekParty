using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.HideAndSeek;
using ShrekParty.Api.Live;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Ceremony;

/// <summary>Игрок на церемонии — зафиксировано на старте.</summary>
public record StandingDto(Guid UserId, string Login, string? AvatarUrl, int Donkeys, int Place, string Achievement);

/// <summary>Итог, замороженный на старте (Event.ResultJson).</summary>
public record CeremonyResult(IReadOnlyList<StandingDto> Standings);

/// <summary>Текущий шаг (Event.StateJson).</summary>
public record CeremonyState(int Step);

/// <summary>Детали церемонии.</summary>
/// <param name="Step">0 — вступление, 1..N — игроки с последнего места, N+1 — пьедестал, N+2 — финальный кадр.</param>
public record CeremonyDetails(int Step, int TotalSteps, IReadOnlyList<StandingDto> Standings);

/// <summary>
/// Церемония награждения: на старте фиксирует таблицу и ачивки, именинник листает шаги. Ослики не начисляет.
/// </summary>
public class CeremonyModule(AppDbContext db, DonkeyService donkeys, ILiveNotifier live) : IEventModule
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public EventType Type => EventType.Ceremony;

    /// <summary>Шагов: вступление + игроки + пьедестал + финал.</summary>
    public static int TotalSteps(int players) => players + 3;

    /// <inheritdoc/>
    public async Task StartAsync(Event ev)
    {
        var board = await donkeys.GetLeaderboardAsync();
        var achievements = Achievements.Assign(await CandidatesAsync(), board.Select(r => r.UserId).ToList());
        var standings = board
            .Select(r => new StandingDto(r.UserId, r.Login, r.AvatarUrl, r.Donkeys, r.Place, achievements[r.UserId]))
            .ToList();
        ev.ResultJson = JsonSerializer.Serialize(new CeremonyResult(standings), Json);
        ev.StateJson = JsonSerializer.Serialize(new CeremonyState(0), Json);
    }

    /// <inheritdoc/>
    public Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var standings = ResultOf(ev).Standings;
        return Task.FromResult<object?>(new CeremonyDetails(StateOf(ev).Step, TotalSteps(standings.Count), standings));
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev) => Task.FromResult<IReadOnlyList<Ranked<Guid>>>([]);

    /// <summary>
    /// Следующий шаг; после финального кадра — <c>end</c>.
    /// </summary>
    public async Task NextAsync(Guid eventId)
    {
        var ev = await db.Events.FirstOrDefaultAsync(e => e.Id == eventId && e.Type == EventType.Ceremony && e.Status == EventStatus.Active)
            ?? throw new EventRuleException("not_active");
        var step = StateOf(ev).Step;
        if (step + 1 >= TotalSteps(ResultOf(ev).Standings.Count))
        {
            throw new EventRuleException("end");
        }

        ev.StateJson = JsonSerializer.Serialize(new CeremonyState(step + 1), Json);
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Кандидаты по ачивкам в порядке важности — только из ивентов, которые были.
    /// </summary>
    private async Task<IReadOnlyList<AchievementCandidates>> CandidatesAsync()
    {
        var tote = await db.DonkeyAwards.Where(a => a.BetId != null)
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Net = g.Sum(a => a.Amount) })
            .ToListAsync();
        var laugh = await db.LaughScores.GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Best = g.Max(s => s.Points) })
            .ToListAsync();
        var likes = await db.PhotoLikes.GroupBy(l => l.OwnerId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync();
        var passwords = (await db.RegistrationAttempts.Where(a => a.UserId != null).ToListAsync())
            .OrderBy(a => a.FirstGuessesLog10)
            .Select(a => a.UserId!.Value)
            .ToList();

        return
        [
            new("Человек-невидимка", await HiddenLongestAsync()),
            new("Главный юморист", laugh.OrderByDescending(x => x.Best).Select(x => x.UserId).ToList()),
            new("Фотогеничный огр", likes.Where(x => x.Count > 0).OrderByDescending(x => x.Count).Select(x => x.UserId).ToList()),
            new("Ставочник года", tote.Where(x => x.Net > 0).OrderByDescending(x => x.Net).Select(x => x.UserId).ToList()),
            new("Банкрот", tote.Where(x => x.Net < 0).OrderBy(x => x.Net).Select(x => x.UserId).ToList()),
            new("Простой пароль", passwords),
        ];
    }

    /// <summary>Кто реже всех попадал на фото в прятках; без сыгранных пряток — никто.</summary>
    private async Task<IReadOnlyList<Guid>> HiddenLongestAsync()
    {
        var lastHideStart = await db.Events.Where(e => e.Type == EventType.HideAndSeek && e.Status == EventStatus.Finished)
            .MaxAsync(e => e.StartedAt);
        if (lastHideStart is null)
        {
            return [];
        }

        var hits = await db.HideShotPlayers.GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count);
        // Пришедшие после пряток не прятались — 0 попаданий у них ничего не значит.
        var players = await db.Users.Where(u => u.Role == UserRole.Player && u.CreatedAt <= lastHideStart).Select(u => u.Id).ToListAsync();
        return players.OrderBy(p => hits.GetValueOrDefault(p)).ToList();
    }

    private static CeremonyResult ResultOf(Event ev) => JsonSerializer.Deserialize<CeremonyResult>(ev.ResultJson!, Json)!;

    private static CeremonyState StateOf(Event ev) => JsonSerializer.Deserialize<CeremonyState>(ev.StateJson!, Json)!;
}
