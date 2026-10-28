namespace ShrekParty.Api.Data.Entities;

/// <summary>
/// Начисление (или списание) осликов игроку.
/// </summary>
public class DonkeyAward
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Ивент, за который начислено; <c>null</c> — вне ивентов (стартовый капитал, аватарка, ручное).</summary>
    public Guid? EventId { get; set; }

    /// <summary>Ставка тотализатора, к которой относится строка (списание, выплата, возврат).</summary>
    public Guid? BetId { get; set; }

    public int Amount { get; set; }

    public required string Reason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
