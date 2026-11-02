namespace ShrekParty.Api.Neuro;

/// <summary>
/// Догадка игрока по одному раунду: кто замаскирован и под кого. Каждое можно выбрать отдельно.
/// </summary>
public class NeuroGuess
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    /// <summary>Раунд из <see cref="NeuroCatalog"/>.</summary>
    public required string RoundId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Имя из <see cref="NeuroCatalog.Names"/>; null — ещё не выбрал.</summary>
    public string? Person { get; set; }

    /// <summary>Персонаж из <see cref="NeuroCatalog.Characters"/>; null — ещё не выбрал.</summary>
    public string? Character { get; set; }
}
