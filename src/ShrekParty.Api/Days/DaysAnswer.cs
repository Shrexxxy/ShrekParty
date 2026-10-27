namespace ShrekParty.Api.Days;

/// <summary>
/// Ответ игрока в «Сколько мне дней» и метка дисквалификации.
/// </summary>
public class DaysAnswer
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public int? Days { get; set; }

    /// <summary>Свернул браузер, переключил вкладку или потерял связь во время ответа.</summary>
    public bool Disqualified { get; set; }
}
