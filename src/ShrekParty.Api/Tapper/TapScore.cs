namespace ShrekParty.Api.Tapper;

/// <summary>
/// Сколько натапал игрок в тапалке.
/// </summary>
public class TapScore
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public int Taps { get; set; }
}
