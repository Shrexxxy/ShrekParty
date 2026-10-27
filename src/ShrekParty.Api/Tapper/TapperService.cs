using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Tapper;

/// <summary>
/// Пачки тапов: прибавляются к счёту игрока; экраны узнают о них с тиком, а не на каждую пачку.
/// </summary>
public class TapperService(AppDbContext db, TimeProvider time)
{
    /// <summary>
    /// Больше в одну пачку не бывает: пачка — 250 мс, человек столько не натапает. Защита счётчика от переполнения.
    /// </summary>
    public const int MaxBatch = 100;

    // Пачки идут 4 раза в секунду от каждого — первая вставка и прибавления не должны гоняться.
    private static readonly SemaphoreSlim TapLock = new(1, 1);

    /// <summary>
    /// Прибавить пачку. До старта — <c>not_running</c>, после финиша с запасом на сеть — <c>too_late</c>.
    /// </summary>
    public async Task AddTapsAsync(Guid eventId, Guid userId, int count)
    {
        if (count is <= 0 or > MaxBatch)
        {
            throw new BadInputException("bad_count");
        }

        var ev = await db.ActiveOfTypeAsync(eventId, EventType.Tapper, tracking: false);
        var state = TapperModule.StateOf(ev);
        if (state.Phase != TapperPhase.Running)
        {
            throw new EventRuleException("not_running");
        }
        if (time.GetUtcNow() > state.Deadline + TapperModule.LastBatchGrace)
        {
            throw new EventRuleException("too_late");
        }

        await TapLock.WaitAsync();
        try
        {
            var updated = await db.TapScores.Where(t => t.EventId == eventId && t.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Taps, t => t.Taps + count));
            if (updated == 0)
            {
                db.TapScores.Add(new TapScore { Id = Guid.NewGuid(), EventId = eventId, UserId = userId, Taps = count });
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            TapLock.Release();
        }
    }
}
