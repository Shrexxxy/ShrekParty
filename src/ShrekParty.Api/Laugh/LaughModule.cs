using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Laugh;

/// <summary>Строка таблицы Смехлыста: очки трёх партий (null — не играл) и лучшая.</summary>
public record LaughRowDto(Guid UserId, string Login, string? AvatarUrl, IReadOnlyList<int?> Games, int? Best);

/// <summary>Детали Смехлыста.</summary>
public record LaughDetails(IReadOnlyList<LaughRowDto> Rows);

/// <summary>Ввод очков: points = null — стереть.</summary>
public record LaughScoreRequest(Guid UserId, int Game, int? Points);

/// <summary>
/// Смехлыст (Jackbox): админ вводит очки трёх партий тем, кто играл; в зачёт — лучшая из сыгранных.
/// </summary>
public class LaughModule(AppDbContext db, ILiveNotifier live) : IEventModule
{
    /// <summary>Сколько партий.</summary>
    public const int Games = 3;

    /// <inheritdoc/>
    public EventType Type => EventType.Laugh;

    /// <inheritdoc/>
    public Task StartAsync(Event ev) => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin)
    {
        var scores = await db.LaughScores.AsNoTracking().Where(s => s.EventId == ev.Id).ToListAsync();
        var players = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync();
        var rows = players
            .Select(p =>
            {
                var games = Enumerable.Range(1, Games)
                    .Select(g => scores.FirstOrDefault(s => s.UserId == p.Id && s.Game == g)?.Points)
                    .ToList();
                return new LaughRowDto(p.Id, p.Login, p.ToMe().AvatarUrl, games, games.Max());
            })
            .OrderByDescending(r => r.Best.HasValue)
            .ThenByDescending(r => r.Best)
            .ThenBy(r => r.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new LaughDetails(rows);
    }

    /// <inheritdoc/>
    public Task<bool> TickAsync(Event ev, DateTimeOffset now) => Task.FromResult(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev)
    {
        var best = await db.LaughScores.Where(s => s.EventId == ev.Id)
            .GroupBy(s => s.UserId)
            .Select(g => new { UserId = g.Key, Best = g.Max(s => s.Points) })
            .ToListAsync();
        return DenseRanking.Rank(best, b => b.Best, descending: true)
            .Select(r => new Ranked<Guid>(r.Item.UserId, r.Place))
            .ToList();
    }

    /// <summary>
    /// Ставит, меняет или стирает очки партии. Нарушение — <see cref="EventRuleException"/> / <see cref="BadInputException"/> / <see cref="KeyNotFoundException"/>.
    /// </summary>
    public async Task SetScoreAsync(Guid eventId, LaughScoreRequest request)
    {
        if (request.Game is < 1 or > Games || request.Points < 0)
        {
            throw new BadInputException("bad_score");
        }
        await db.ActiveOfTypeAsync(eventId, EventType.Laugh, tracking: false);
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId && u.Role == UserRole.Player))
        {
            throw new KeyNotFoundException();
        }

        var score = await db.LaughScores.FirstOrDefaultAsync(s => s.EventId == eventId && s.UserId == request.UserId && s.Game == request.Game);
        if (request.Points is null)
        {
            if (score is not null)
            {
                db.LaughScores.Remove(score);
            }
        }
        else if (score is null)
        {
            db.LaughScores.Add(new LaughScore { Id = Guid.NewGuid(), EventId = eventId, UserId = request.UserId, Game = request.Game, Points = request.Points.Value });
        }
        else
        {
            score.Points = request.Points.Value;
        }

        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }
}
