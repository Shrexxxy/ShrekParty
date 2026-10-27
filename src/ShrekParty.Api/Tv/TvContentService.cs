using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Tv;

/// <summary>Сообщение на ТВ в снапшоте.</summary>
public record TvMessageDto(string Text, string? ImageUrl, DateTimeOffset Until);

/// <summary>Шаблон подписи.</summary>
public record CaptionDto(Guid Id, string Text);

/// <summary>Новый шаблон.</summary>
public record CaptionRequest(string? Text);

/// <summary>Мем в админке.</summary>
public record MemeDto(Guid Id, string Url, string? Caption);

/// <summary>
/// Контент ТВ: ручные сообщения, шаблоны подписей к фото матчей, мемы.
/// </summary>
public class TvContentService(AppDbContext db, MediaStorage media, ILiveNotifier live, TimeProvider time)
{
    /// <summary>Тег мемов в медиа.</summary>
    public const string MemeTag = "meme";

    /// <summary>Стартовый набор подписей.</summary>
    public static readonly IReadOnlyList<string> DefaultCaptions =
    [
        "ФОТО ЖОСКОЙ ЗАРУБЫ {A} И {B}",
        "{A} ПРОТИВ {B}: ДО ПОСЛЕДНЕЙ КАПЛИ ПОТА",
        "{A} И {B} — БИТВА ВЕКА",
        "ДЖОЙСТИКИ ДЫМЯТСЯ: {A} VS {B}",
        "ИСТОРИЧЕСКИЙ КАДР: {A} И {B} В ДЕЛЕ",
    ];

    /// <summary>Сколько максимум держать сообщение.</summary>
    public const int MaxSeconds = 600;

    /// <summary>
    /// Стартовые подписи — только если шаблонов ещё нет (правки именинника не перезатираются).
    /// </summary>
    public async Task SeedCaptionsAsync()
    {
        if (await db.CaptionTemplates.AnyAsync())
        {
            return;
        }

        db.CaptionTemplates.AddRange(DefaultCaptions.Select(t => new CaptionTemplate { Id = Guid.NewGuid(), Text = t }));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Новое сообщение заменяет предыдущее.
    /// </summary>
    public async Task PostMessageAsync(string? text, int seconds, Stream? image, string? contentType, Guid authorId)
    {
        if (string.IsNullOrWhiteSpace(text) || seconds is < 1 or > MaxSeconds)
        {
            throw new ArgumentException("bad_message");
        }

        Guid? imageId = null;
        if (image is not null && contentType is not null)
        {
            imageId = (await media.SaveAsync(image, contentType, MediaKind.Photo, authorId, tag: "tvmessage")).Id;
        }

        await db.TvMessages.ExecuteDeleteAsync();
        var now = time.GetUtcNow();
        db.TvMessages.Add(new TvMessage { Id = Guid.NewGuid(), Text = text.Trim(), ImageMediaId = imageId, Until = now.AddSeconds(seconds), CreatedAt = now });
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Убрать сообщение с экрана.
    /// </summary>
    public async Task ClearMessageAsync()
    {
        await db.TvMessages.ExecuteDeleteAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Текущее сообщение, если ещё не истекло.
    /// </summary>
    public async Task<TvMessageDto?> GetActiveMessageAsync()
    {
        var now = time.GetUtcNow();
        var message = (await db.TvMessages.AsNoTracking().ToListAsync()).Where(m => m.Until > now).MaxBy(m => m.CreatedAt);
        return message is null
            ? null
            : new TvMessageDto(message.Text, message.ImageMediaId is { } img ? MediaStorage.UrlFor(img) : null, message.Until);
    }

    /// <summary>Все шаблоны подписей.</summary>
    public async Task<IReadOnlyList<CaptionDto>> GetCaptionsAsync() =>
        await db.CaptionTemplates.AsNoTracking().OrderBy(c => c.Text).Select(c => new CaptionDto(c.Id, c.Text)).ToListAsync();

    /// <summary>Добавить шаблон.</summary>
    public async Task<CaptionDto> AddCaptionAsync(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("bad_caption");
        }

        var caption = new CaptionTemplate { Id = Guid.NewGuid(), Text = text.Trim() };
        db.CaptionTemplates.Add(caption);
        await db.SaveChangesAsync();
        return new CaptionDto(caption.Id, caption.Text);
    }

    /// <summary>Удалить шаблон.</summary>
    public async Task<bool> DeleteCaptionAsync(Guid id) => await db.CaptionTemplates.Where(c => c.Id == id).ExecuteDeleteAsync() > 0;

    /// <summary>Загрузить мем.</summary>
    public async Task AddMemeAsync(Stream content, string contentType, string? caption, Guid authorId)
    {
        await media.SaveAsync(content, contentType, MediaKind.Photo, authorId, caption: string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(), tag: MemeTag);
        await db.SaveChangesAsync();
    }

    /// <summary>Все мемы.</summary>
    public async Task<IReadOnlyList<MemeDto>> GetMemesAsync() =>
        (await db.MediaFiles.AsNoTracking().Where(m => m.Tag == MemeTag).ToListAsync())
        .OrderBy(m => m.CreatedAt)
        .Select(m => new MemeDto(m.Id, MediaStorage.UrlFor(m.Id), m.Caption))
        .ToList();

    /// <summary>Удалить мем вместе с файлом.</summary>
    public async Task<bool> DeleteMemeAsync(Guid id)
    {
        var meme = await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == id && m.Tag == MemeTag);
        if (meme is null)
        {
            return false;
        }

        File.Delete(media.PathFor(meme));
        db.MediaFiles.Remove(meme);
        await db.SaveChangesAsync();
        return true;
    }
}
