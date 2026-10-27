namespace ShrekParty.Api.Laugh;

/// <summary>
/// Очки игрока в одной партии Смехлыста (вводит админ).
/// </summary>
public class LaughScore
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Номер партии, 1–3.</summary>
    public int Game { get; set; }

    public int Points { get; set; }
}
