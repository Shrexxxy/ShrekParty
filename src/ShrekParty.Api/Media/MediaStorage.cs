using Microsoft.Extensions.Options;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Media;

/// <summary>
/// Хранение медиафайлов на диске (<c>{DataDir}/media</c>) с записью в базе.
/// </summary>
public class MediaStorage(IOptions<StorageOptions> storage, AppDbContext db, TimeProvider time)
{
    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/heic"] = ".heic",
        ["image/gif"] = ".gif",
        ["audio/mpeg"] = ".mp3",
        ["audio/mp4"] = ".m4a",
        ["audio/ogg"] = ".ogg",
        ["audio/wav"] = ".wav",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/quicktime"] = ".mov",
    };

    /// <summary>Папка с файлами.</summary>
    public string MediaDir => Path.Combine(storage.Value.DataDir, "media");

    /// <summary>
    /// Ссылка на файл для клиентов.
    /// </summary>
    public static string UrlFor(Guid mediaId) => $"/media/{mediaId}";

    /// <summary>
    /// Сохраняет файл и запись о нём. Сохранение в базу — на вызывающем (<c>SaveChangesAsync</c>).
    /// </summary>
    public async Task<MediaFile> SaveAsync(
        Stream content,
        string contentType,
        MediaKind kind,
        Guid? authorId,
        Guid? userId = null,
        Guid? eventId = null,
        string? caption = null,
        Guid? matchId = null,
        string? tag = null)
    {
        Directory.CreateDirectory(MediaDir);
        var id = Guid.NewGuid();
        var fileName = id.ToString("N") + Extensions.GetValueOrDefault(contentType, ".bin");

        await using (var file = File.Create(Path.Combine(MediaDir, fileName)))
        {
            await content.CopyToAsync(file);
        }

        var media = new MediaFile
        {
            Id = id,
            Kind = kind,
            FileName = fileName,
            ContentType = contentType,
            AuthorId = authorId,
            UserId = userId,
            EventId = eventId,
            Caption = caption,
            MatchId = matchId,
            Tag = tag,
            CreatedAt = time.GetUtcNow(),
        };
        db.MediaFiles.Add(media);
        return media;
    }

    /// <summary>
    /// Полный путь к файлу медиа.
    /// </summary>
    public string PathFor(MediaFile media) => Path.Combine(MediaDir, media.FileName);
}
