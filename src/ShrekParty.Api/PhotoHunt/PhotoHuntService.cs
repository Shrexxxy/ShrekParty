using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.PhotoHunt;

/// <summary>
/// Фото игроков (до дедлайна, с заменой) и лайки после дедлайна.
/// </summary>
public class PhotoHuntService(AppDbContext db, MediaStorage media, ILiveNotifier live, TimeProvider time)
{
    // «Переснять», пока первое фото ещё грузится, — два запроса разом; без блокировки вторая вставка падает на уникальном индексе.
    private static readonly SemaphoreSlim SubmitLock = new(1, 1);

    /// <summary>
    /// Сдать или переснять фото. После дедлайна — <c>too_late</c>, даже если тик ещё не переключил фазу.
    /// </summary>
    public async Task SubmitAsync(Guid eventId, Guid userId, Stream content, string contentType)
    {
        var ev = await ActiveAsync(eventId);
        var state = PhotoHuntModule.StateOf(ev);
        if (state.Phase != PhotoPhase.Shooting || time.GetUtcNow() >= state.Deadline)
        {
            throw new EventRuleException("too_late");
        }

        await SubmitLock.WaitAsync();
        try
        {
            var file = await media.SaveAsync(content, contentType, MediaKind.Photo, userId, userId: userId, eventId: eventId, tag: "photohunt");
            var submission = await db.PhotoSubmissions.FirstOrDefaultAsync(s => s.EventId == eventId && s.UserId == userId);
            if (submission is null)
            {
                db.PhotoSubmissions.Add(new PhotoSubmission { Id = Guid.NewGuid(), EventId = eventId, UserId = userId, MediaId = file.Id, SubmittedAt = time.GetUtcNow() });
            }
            else
            {
                // Пересняли — старый файл больше не нужен.
                var old = await db.MediaFiles.FindAsync(submission.MediaId);
                if (old is not null)
                {
                    File.Delete(media.PathFor(old));
                    db.MediaFiles.Remove(old);
                }

                submission.MediaId = file.Id;
                submission.SubmittedAt = time.GetUtcNow();
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            SubmitLock.Release();
        }

        await live.BroadcastAsync();
    }

    /// <summary>
    /// Лайк чужому фото — только после дедлайна; повторный лайк ничего не меняет.
    /// </summary>
    public async Task LikeAsync(Guid eventId, Guid likerId, Guid ownerId)
    {
        await CheckLikeAsync(eventId, likerId, ownerId);
        if (!await db.PhotoLikes.AnyAsync(l => l.EventId == eventId && l.OwnerId == ownerId && l.LikerId == likerId))
        {
            db.PhotoLikes.Add(new PhotoLike { Id = Guid.NewGuid(), EventId = eventId, OwnerId = ownerId, LikerId = likerId, CreatedAt = time.GetUtcNow() });
            await db.SaveChangesAsync();
        }

        await live.BroadcastAsync();
    }

    /// <summary>
    /// Снять лайк.
    /// </summary>
    public async Task UnlikeAsync(Guid eventId, Guid likerId, Guid ownerId)
    {
        await CheckLikeAsync(eventId, likerId, ownerId);
        await db.PhotoLikes.Where(l => l.EventId == eventId && l.OwnerId == ownerId && l.LikerId == likerId).ExecuteDeleteAsync();
        await live.BroadcastAsync();
    }

    private async Task CheckLikeAsync(Guid eventId, Guid likerId, Guid ownerId)
    {
        var ev = await ActiveAsync(eventId);
        var state = PhotoHuntModule.StateOf(ev);
        if (state.Phase != PhotoPhase.Rating && time.GetUtcNow() < state.Deadline)
        {
            throw new EventRuleException("not_yet");
        }
        if (likerId == ownerId)
        {
            throw new EventRuleException("own_photo");
        }
        if (!await db.PhotoSubmissions.AnyAsync(s => s.EventId == eventId && s.UserId == ownerId))
        {
            throw new KeyNotFoundException();
        }
    }

    private async Task<Event> ActiveAsync(Guid eventId) =>
        await db.ActiveOfTypeAsync(eventId, EventType.PhotoHunt);
}
