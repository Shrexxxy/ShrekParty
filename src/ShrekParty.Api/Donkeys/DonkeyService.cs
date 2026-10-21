using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Live;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Donkeys;

/// <summary>
/// Начисление осликов и таблица лидеров.
/// </summary>
public class DonkeyService(AppDbContext db, TimeProvider time, ILiveNotifier live)
{
    /// <summary>
    /// Начисляет (или списывает при отрицательном) ослики игроку.
    /// </summary>
    public async Task AwardAsync(Guid userId, int amount, string reason, Guid? eventId = null)
    {
        Add(userId, amount, reason, eventId);
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Переводит места рейтинга в ослики по схеме и начисляет. Нулевые награды не пишутся.
    /// </summary>
    public async Task AwardRankingAsync(
        Guid eventId,
        IReadOnlyList<Ranked<Guid>> ranking,
        ScoringScheme scheme,
        int maxDonkeys,
        string reason,
        IReadOnlyDictionary<int, int>? customTable = null)
    {
        if (ranking.Count == 0)
        {
            return;
        }

        var distinctPlaces = ranking.Max(r => r.Place);
        foreach (var (userId, place) in ranking)
        {
            var amount = PointsCalculator.ForPlace(scheme, place, distinctPlaces, maxDonkeys, customTable);
            if (amount != 0)
            {
                Add(userId, amount, reason, eventId);
            }
        }

        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Таблица лидеров: все игроки (с нулём, если наград нет), плотные места по сумме.
    /// </summary>
    /// <param name="eventId">Только награды этого ивента; <c>null</c> — общая.</param>
    public async Task<IReadOnlyList<LeaderboardRowDto>> GetLeaderboardAsync(Guid? eventId = null)
    {
        var players = await db.Users.AsNoTracking().Where(u => u.Role == UserRole.Player).ToListAsync();
        var awards = db.DonkeyAwards.AsNoTracking();
        if (eventId is not null)
        {
            awards = awards.Where(a => a.EventId == eventId);
        }

        var sums = await awards
            .GroupBy(a => a.UserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(x => x.UserId, x => x.Total);

        var ranked = DenseRanking.Rank(players, p => sums.GetValueOrDefault(p.Id), descending: true);

        return ranked
            .Select(r => new LeaderboardRowDto(
                r.Item.Id,
                r.Item.Login,
                r.Item.ToMe().AvatarUrl,
                sums.GetValueOrDefault(r.Item.Id),
                r.Place))
            .OrderBy(r => r.Place)
            .ThenBy(r => r.Login, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void Add(Guid userId, int amount, string reason, Guid? eventId) =>
        db.DonkeyAwards.Add(new DonkeyAward
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventId = eventId,
            Amount = amount,
            Reason = reason,
            CreatedAt = time.GetUtcNow(),
        });
}
