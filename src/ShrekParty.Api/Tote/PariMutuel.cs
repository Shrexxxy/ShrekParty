namespace ShrekParty.Api.Tote;

/// <summary>Ставка для расчёта.</summary>
public record PariMutuelBet(Guid BetId, Guid OnPlayerId, int Amount);

/// <summary>
/// Пари-мутюэль: весь банк матча делится между угадавшими пропорционально ставкам.
/// </summary>
public static class PariMutuel
{
    /// <summary>
    /// Выплаты по ставкам (ставка → сколько вернуть). Никто не угадал — каждому возврат его ставки.
    /// Остаток от округления вниз — крупнейшей выигравшей ставке, чтобы банк сошёлся до ослика.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> Settle(IReadOnlyList<PariMutuelBet> bets, Guid winnerId)
    {
        var winners = bets.Where(b => b.OnPlayerId == winnerId).ToList();
        if (winners.Count == 0)
        {
            return bets.ToDictionary(b => b.BetId, b => b.Amount);
        }

        var pool = bets.Sum(b => b.Amount);
        var winningSum = winners.Sum(b => b.Amount);
        var payouts = winners.ToDictionary(b => b.BetId, b => (int)((long)pool * b.Amount / winningSum));
        var biggest = winners.OrderByDescending(b => b.Amount).First();
        payouts[biggest.BetId] += pool - payouts.Values.Sum();
        return payouts;
    }

    /// <summary>
    /// Коэффициент на игрока: банк / сумма ставок на него; null — на него никто не ставил.
    /// </summary>
    public static double? Odds(IReadOnlyList<PariMutuelBet> bets, Guid playerId)
    {
        var side = bets.Where(b => b.OnPlayerId == playerId).Sum(b => b.Amount);
        return side == 0 ? null : Math.Round((double)bets.Sum(b => b.Amount) / side, 2);
    }
}
