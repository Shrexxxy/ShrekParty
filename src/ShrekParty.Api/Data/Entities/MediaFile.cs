namespace ShrekParty.Api.Data.Entities;

/// <summary>
/// Вид медиафайла.
/// </summary>
public enum MediaKind
{
    Photo,
    Audio,
    Video,
}

/// <summary>
/// Загруженный медиафайл и его привязки.
/// </summary>
public class MediaFile
{
    public Guid Id { get; set; }

    public MediaKind Kind { get; set; }

    /// <summary>Имя файла в папке медиа.</summary>
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public Guid? AuthorId { get; set; }

    /// <summary>Игрок, к которому относится файл (например, аватарка).</summary>
    public Guid? UserId { get; set; }

    public Guid? EventId { get; set; }

    /// <summary>Матч турнира — для подписей «ЖОСКАЯ ЗАРУБА {A} И {B}».</summary>
    public Guid? MatchId { get; set; }

    /// <summary>Назначение: match, meme, hide, photohunt… — для карусели ТВ.</summary>
    public string? Tag { get; set; }

    public string? Caption { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
