using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.HideAndSeek;

/// <summary>
/// Действия в прятках: снимки и отметки (именинник), досрочный конец съёмки, голос за смешное фото.
/// </summary>
public class HideSeekService(AppDbContext db, MediaStorage media, EventEngine engine, ILiveNotifier live, TimeProvider time)
{
    // Два быстрых тапа по разным фото — два запроса разом; без блокировки оба вставят голос и упрутся в уникальный индекс.
    private static readonly SemaphoreSlim VoteLock = new(1, 1);

    /// <summary>
    /// Снимок со списком попавших; пустой список — фото без людей, тоже хранится.
    /// </summary>
    /// <exception cref="BadInputException">Среди отмеченных есть не-игрок.</exception>
    public async Task<Guid> AddShotAsync(Guid eventId, IReadOnlyCollection<Guid> userIds, Stream photo, string contentType, Guid adminId)
    {
        var ev = await ActiveAsync(eventId);
        if (HideSeekModule.StateOf(ev).Phase != HidePhase.Shooting)
        {
            throw new EventRuleException("not_shooting");
        }

        await EnsurePlayersAsync(userIds);
        var file = await media.SaveAsync(photo, contentType, MediaKind.Photo, adminId, eventId: eventId, tag: HideSeekModule.MediaTag);
        var shot = new HideShot { Id = Guid.NewGuid(), EventId = eventId, MediaId = file.Id, TakenAt = time.GetUtcNow() };
        db.HideShots.Add(shot);
        db.HideShotPlayers.AddRange(Marks(shot.Id, userIds));
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
        return shot.Id;
    }

    /// <summary>
    /// Переотметить, кто на снимке; можно до конца ивента.
    /// </summary>
    public async Task SetPlayersAsync(Guid shotId, IReadOnlyCollection<Guid> userIds)
    {
        var shot = await db.HideShots.FirstOrDefaultAsync(s => s.Id == shotId) ?? throw new KeyNotFoundException();
        await ActiveAsync(shot.EventId);
        await EnsurePlayersAsync(userIds);

        await db.HideShotPlayers.Where(p => p.ShotId == shotId).ExecuteDeleteAsync();
        db.HideShotPlayers.AddRange(Marks(shotId, userIds));
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Удалить неудачный снимок; с началом голосования — нельзя.
    /// </summary>
    public async Task DeleteShotAsync(Guid shotId)
    {
        var shot = await db.HideShots.FirstOrDefaultAsync(s => s.Id == shotId) ?? throw new KeyNotFoundException();
        var ev = await ActiveAsync(shot.EventId);
        if (HideSeekModule.StateOf(ev).Phase == HidePhase.Voting)
        {
            throw new EventRuleException("voting_started");
        }

        await db.HideShotPlayers.Where(p => p.ShotId == shotId).ExecuteDeleteAsync();
        db.HideShots.Remove(shot);
        if (await db.MediaFiles.FirstOrDefaultAsync(m => m.Id == shot.MediaId) is { } file)
        {
            File.Delete(media.PathFor(file));
            db.MediaFiles.Remove(file);
        }

        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Закончить съёмку раньше — сразу голосование; без снимков — сразу итоги.
    /// </summary>
    public async Task StopShootingAsync(Guid eventId)
    {
        var ev = await ActiveAsync(eventId);
        if (HideSeekModule.StateOf(ev).Phase != HidePhase.Shooting)
        {
            throw new EventRuleException("not_shooting");
        }

        if (!await HideSeekModule.HasShotsAsync(db, eventId))
        {
            await engine.FinishAsync(eventId);
            return;
        }

        HideSeekModule.StartVoting(ev, time.GetUtcNow());
        await db.SaveChangesAsync();
        await live.BroadcastAsync();
    }

    /// <summary>
    /// Голос за самое смешное фото; можно передумать.
    /// </summary>
    public async Task VoteAsync(Guid eventId, Guid shotId, Guid voterId)
    {
        var ev = await ActiveAsync(eventId);
        if (HideSeekModule.StateOf(ev).Phase != HidePhase.Voting)
        {
            throw new EventRuleException("not_voting");
        }

        if (!await db.HideShots.AnyAsync(s => s.Id == shotId && s.EventId == eventId))
        {
            throw new KeyNotFoundException();
        }

        await VoteLock.WaitAsync();
        try
        {
            var vote = await db.HideVotes.FirstOrDefaultAsync(v => v.EventId == eventId && v.VoterId == voterId);
            if (vote is null)
            {
                db.HideVotes.Add(new HideVote { Id = Guid.NewGuid(), EventId = eventId, VoterId = voterId, ShotId = shotId });
            }
            else
            {
                vote.ShotId = shotId;
            }

            await db.SaveChangesAsync();
        }
        finally
        {
            VoteLock.Release();
        }

        await live.BroadcastAsync();
    }

    private static IEnumerable<HideShotPlayer> Marks(Guid shotId, IEnumerable<Guid> userIds) =>
        userIds.Distinct().Select(u => new HideShotPlayer { Id = Guid.NewGuid(), ShotId = shotId, UserId = u });

    private async Task EnsurePlayersAsync(IReadOnlyCollection<Guid> userIds)
    {
        var distinct = userIds.Distinct().ToList();
        var found = await db.Users.CountAsync(u => distinct.Contains(u.Id) && u.Role == UserRole.Player);
        if (found != distinct.Count)
        {
            throw new BadInputException("not_a_player");
        }
    }

    private async Task<Event> ActiveAsync(Guid eventId) =>
        await db.ActiveOfTypeAsync(eventId, EventType.HideAndSeek);
}
