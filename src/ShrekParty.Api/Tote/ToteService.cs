using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Tournament;

namespace ShrekParty.Api.Tote;

/// <summary>
/// Тотализатор: ставки на матчи турнира и расчёт после результата.
/// </summary>
public class ToteService(AppDbContext db, ILiveNotifier live, TimeProvider time)
{
    /// <summary>Максимальная ставка на матч.</summary>
    public const int MaxBet = 25;

    public const string StakeReason = "Ставка";
    public const string PayoutReason = "Выигрыш ставки";
    public const string RefundReason = "Возврат ставки";

    // Сервер один — блокировки в процессе хватает, чтобы две быстрые ставки не увели баланс в минус.
    private static readonly SemaphoreSlim BetLock = new(1, 1);

    /// <summary>
    /// Ставка игрока: списывается сразу. Нарушение правил — <see cref="EventRuleException"/>, неверные данные — <see cref="ArgumentException"/>.
    /// </summary>
    public async Task PlaceBetAsync(Guid matchId, Guid userId, Guid onPlayerId, int amount)
    {
        if (amount < 1)
        {
            throw new ArgumentException("bad_amount");
        }
        if (amount > MaxBet)
        {
            throw new EventRuleException("limit");
        }

        await BetLock.WaitAsync();
        try
        {
            var match = await db.TournamentMatches.FirstOrDefaultAsync(m => m.Id == matchId) ?? throw new KeyNotFoundException();
            var tote = await ToteOfAsync(match.EventId);
            if (match.Status != MatchStatus.Pending || match.PlayerAId is null || match.PlayerBId is null || tote is null)
            {
                throw new EventRuleException("closed");
            }
            if (userId == match.PlayerAId || userId == match.PlayerBId)
            {
                throw new EventRuleException("own_match");
            }
            if (onPlayerId != match.PlayerAId && onPlayerId != match.PlayerBId)
            {
                throw new ArgumentException("not_in_match");
            }
            if (await db.Bets.AnyAsync(b => b.MatchId == matchId && b.UserId == userId))
            {
                throw new EventRuleException("already");
            }

            var balance = await db.DonkeyAwards.Where(a => a.UserId == userId).SumAsync(a => a.Amount);
            if (balance < amount)
            {
                throw new EventRuleException("balance");
            }

            var bet = new Bet { Id = Guid.NewGuid(), MatchId = matchId, UserId = userId, OnPlayerId = onPlayerId, Amount = amount, CreatedAt = time.GetUtcNow() };
            db.Bets.Add(bet);
            db.DonkeyAwards.Add(Award(bet, tote.Id, -amount, StakeReason));
            await db.SaveChangesAsync();
        }
        finally
        {
            BetLock.Release();
        }

        await live.BroadcastAsync();
    }

    /// <summary>
    /// Выплаты по матчу (вызывается при результате). Повторный вызов ничего не задваивает.
    /// Сохранение — на вызывающем.
    /// </summary>
    public async Task SettleAsync(TournamentMatch match)
    {
        var bets = await db.Bets.Where(b => b.MatchId == match.Id).ToListAsync();
        var tote = await ToteOfAsync(match.EventId);
        if (bets.Count == 0 || match.WinnerId is not { } winner || tote is null)
        {
            return;
        }

        var betIds = bets.Select(b => b.Id).ToList();
        if (await db.DonkeyAwards.AnyAsync(a => a.BetId != null && betIds.Contains(a.BetId.Value) && a.Amount > 0))
        {
            return;
        }

        var nobodyWon = bets.All(b => b.OnPlayerId != winner);
        var payouts = PariMutuel.Settle(bets.Select(b => new PariMutuelBet(b.Id, b.OnPlayerId, b.Amount)).ToList(), winner);
        foreach (var bet in bets.Where(b => payouts.GetValueOrDefault(b.Id) > 0))
        {
            db.DonkeyAwards.Add(Award(bet, tote.Id, payouts[bet.Id], nobodyWon ? RefundReason : PayoutReason));
        }
    }

    /// <summary>
    /// Отмена выплат по матчу (отмена результата). Списания ставок остаются — ставки снова в игре.
    /// </summary>
    public async Task UnsettleAsync(TournamentMatch match)
    {
        var betIds = await db.Bets.Where(b => b.MatchId == match.Id).Select(b => b.Id).ToListAsync();
        var payouts = await db.DonkeyAwards
            .Where(a => a.BetId != null && betIds.Contains(a.BetId.Value) && a.Amount > 0)
            .ToListAsync();
        db.DonkeyAwards.RemoveRange(payouts);
    }

    private Task<Event?> ToteOfAsync(Guid tournamentId) =>
        db.Events.FirstOrDefaultAsync(e => e.ParentId == tournamentId && e.Type == EventType.Tote && e.Status == EventStatus.Active);

    private DonkeyAward Award(Bet bet, Guid toteId, int amount, string reason) => new()
    {
        Id = Guid.NewGuid(),
        UserId = bet.UserId,
        EventId = toteId,
        BetId = bet.Id,
        Amount = amount,
        Reason = reason,
        CreatedAt = time.GetUtcNow(),
    };
}
