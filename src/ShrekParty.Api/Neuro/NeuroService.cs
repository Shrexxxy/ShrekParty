using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Neuro;

/// <summary>Нейрофото в админке.</summary>
public record NeuroPhotoDto(Guid Id, string Url, Guid? SubjectId);

/// <summary>
/// Контент нейрофото (загрузка заранее, отметка «кто на фото») и догадки игроков.
/// </summary>
public class NeuroService(AppDbContext db, MediaStorage media, ILiveNotifier live, TimeProvider time)
{
    /// <summary>Тег медиа нейрофото — сброс их не трогает.</summary>
    public const string MediaTag = "neuro";

    // Два быстрых тапа по вариантам — два запроса разом; без блокировки оба вставят догадку.
    private static readonly SemaphoreSlim GuessLock = new(1, 1);

    /// <summary>Все фото в порядке показа.</summary>
    public async Task<IReadOnlyList<NeuroPhotoDto>> GetPhotosAsync() =>
        (await db.NeuroPhotos.AsNoTracking().ToListAsync())
        .OrderBy(p => p.CreatedAt)
        .Select(p => new NeuroPhotoDto(p.Id, MediaStorage.UrlFor(p.MediaId), p.SubjectId))
        .ToList();

    /// <summary>Загрузить фото (без отметки).</summary>
    public async Task<Guid> UploadAsync(Stream content, string contentType, Guid adminId)
    {
        var file = await media.SaveAsync(content, contentType, MediaKind.Photo, adminId, tag: MediaTag);
        var photo = new NeuroPhoto { Id = Guid.NewGuid(), MediaId = file.Id, CreatedAt = time.GetUtcNow() };
        db.NeuroPhotos.Add(photo);
        await db.SaveChangesAsync();
        return photo.Id;
    }

    /// <summary>Отметить, кто на фото (игрок или именинник); <c>null</c> — снять отметку.</summary>
    public async Task MarkAsync(Guid photoId, Guid? subjectId)
    {
        var photo = await db.NeuroPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new KeyNotFoundException();
        if (subjectId is { } id && !await db.Users.AnyAsync(u => u.Id == id && (u.Role == UserRole.Player || u.Role == UserRole.Admin)))
        {
            throw new BadInputException("not_a_player");
        }

        photo.SubjectId = subjectId;
        await db.SaveChangesAsync();
    }

    /// <summary>Удалить фото вместе с файлом; пока идёт игра — нельзя.</summary>
    public async Task DeleteAsync(Guid photoId)
    {
        var photo = await db.NeuroPhotos.FirstOrDefaultAsync(p => p.Id == photoId) ?? throw new KeyNotFoundException();
        if (await db.Events.AnyAsync(e => e.Type == EventType.NeuroPhoto && e.Status == EventStatus.Active))
        {
            throw new EventRuleException("in_game");
        }

        if (await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == photo.MediaId) is { } file)
        {
            File.Delete(media.PathFor(file));
            db.MediaFiles.Remove(file);
        }

        db.NeuroPhotos.Remove(photo);
        await db.SaveChangesAsync();
    }

    /// <summary>Догадка по текущему фото; можно передумать до конца времени.</summary>
    public async Task GuessAsync(Guid eventId, Guid userId, Guid guessId)
    {
        await GuessLock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.NeuroPhoto, tracking: false);
            var state = NeuroModule.StateOf(ev);
            if (state.Phase != NeuroPhase.Guessing || time.GetUtcNow() >= state.Deadline)
            {
                throw new EventRuleException("too_late");
            }
            if ((await NeuroModule.OptionsAsync(db)).All(o => o.UserId != guessId))
            {
                throw new BadInputException("not_a_player");
            }

            var photoId = state.PhotoIds[state.Index];
            var guess = await db.NeuroGuesses.FirstOrDefaultAsync(g => g.EventId == eventId && g.PhotoId == photoId && g.UserId == userId);
            if (guess is null)
            {
                db.NeuroGuesses.Add(new NeuroGuess { Id = Guid.NewGuid(), EventId = eventId, PhotoId = photoId, UserId = userId, GuessId = guessId });
            }
            else
            {
                guess.GuessId = guessId;
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            GuessLock.Release();
        }

        await live.BroadcastAsync();
    }
}
