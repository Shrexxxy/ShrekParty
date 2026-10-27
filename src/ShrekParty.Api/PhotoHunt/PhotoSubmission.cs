namespace ShrekParty.Api.PhotoHunt;

/// <summary>
/// Фото игрока на задание фотоохоты (одно — переснять можно до дедлайна).
/// </summary>
public class PhotoSubmission
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public Guid MediaId { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }
}
