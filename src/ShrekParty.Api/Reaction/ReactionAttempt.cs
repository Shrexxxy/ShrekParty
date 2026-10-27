namespace ShrekParty.Api.Reaction;

/// <summary>
/// Попытка игрока в раунде реакции: время или фальстарт.
/// </summary>
public class ReactionAttempt
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public int Round { get; set; }

    /// <summary>Время реакции по часам телефона; <c>null</c> — фальстарт.</summary>
    public int? Ms { get; set; }
}
