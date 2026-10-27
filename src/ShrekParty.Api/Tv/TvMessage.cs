namespace ShrekParty.Api.Tv;

/// <summary>
/// Ручное сообщение именинника на весь экран ТВ («Торт через 5 минут!»). Показывается последнее, пока не истекло.
/// </summary>
public class TvMessage
{
    public Guid Id { get; set; }

    public required string Text { get; set; }

    public Guid? ImageMediaId { get; set; }

    public DateTimeOffset Until { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Шаблон подписи к фото матча: {A} и {B} заменяются игроками.
/// </summary>
public class CaptionTemplate
{
    public Guid Id { get; set; }

    public required string Text { get; set; }
}
