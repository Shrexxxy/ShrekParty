using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Reaction;

/// <summary>
/// Попытки реакции: время в зелёном, фальстарт в красном; по одной на раунд.
/// </summary>
public class ReactionService(AppDbContext db, ILiveNotifier live)
{
    // Двойной тап — два запроса разом; без блокировки оба пройдут проверку «ещё не было попытки».
    private static readonly SemaphoreSlim AttemptLock = new(1, 1);

    /// <summary>
    /// Время реакции текущего раунда (меряет телефон).
    /// </summary>
    public Task AttemptAsync(Guid eventId, Guid userId, int ms)
    {
        if (ms <= 0)
        {
            throw new BadInputException("bad_time");
        }

        return SaveAsync(eventId, userId, ReactionPhase.Green, ms);
    }

    /// <summary>
    /// Тап до зелёного — раунд сгорел.
    /// </summary>
    public Task FalseStartAsync(Guid eventId, Guid userId) => SaveAsync(eventId, userId, ReactionPhase.Red, null);

    private async Task SaveAsync(Guid eventId, Guid userId, ReactionPhase phase, int? ms)
    {
        await AttemptLock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.Reaction);
            var state = ReactionModule.StateOf(ev);
            if (state.Phase != phase)
            {
                throw new EventRuleException(phase == ReactionPhase.Green ? "not_green" : "already_green");
            }
            if (await db.ReactionAttempts.AnyAsync(a => a.EventId == eventId && a.UserId == userId && a.Round == state.Round))
            {
                throw new EventRuleException("already_tried");
            }

            db.ReactionAttempts.Add(new ReactionAttempt { Id = Guid.NewGuid(), EventId = eventId, UserId = userId, Round = state.Round, Ms = ms });
            await db.SaveChangesAsync();
        }
        finally
        {
            AttemptLock.Release();
        }

        await live.BroadcastAsync();
    }
}
