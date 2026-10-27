namespace ShrekParty.Api.Tote;

/// <summary>
/// Ставка на матч. Деньги — строки DonkeyAward с этим BetId: списание (−), выплата или возврат (+).
/// </summary>
public class Bet
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    public Guid UserId { get; set; }

    public Guid OnPlayerId { get; set; }

    public int Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
