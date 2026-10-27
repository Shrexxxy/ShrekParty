namespace ShrekParty.Api.PhotoHunt;

/// <summary>
/// Лайк игрока фото другого игрока в одном запуске фотоохоты.
/// </summary>
public class PhotoLike
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    /// <summary>Чьё фото.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>Кто лайкнул.</summary>
    public Guid LikerId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
