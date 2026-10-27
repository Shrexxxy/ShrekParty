using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Days;

/// <summary>
/// Ответы и дисквалификации в «Сколько мне дней».
/// </summary>
public class DaysService(AppDbContext db, ILiveNotifier live, TimeProvider time)
{
    // Ответ и «свернул браузер» могут прийти от одного игрока одновременно.
    private static readonly SemaphoreSlim AnswerLock = new(1, 1);

    /// <summary>
    /// Ответ (можно менять до дедлайна); дисквалифицированному — <c>disqualified</c>.
    /// </summary>
    public async Task AnswerAsync(Guid eventId, Guid userId, int days)
    {
        if (days < 0)
        {
            throw new BadInputException("bad_days");
        }

        await UpsertAsync(eventId, userId, answer =>
        {
            if (answer.Disqualified)
            {
                throw new EventRuleException("disqualified");
            }

            answer.Days = days;
        });
    }

    /// <summary>
    /// Телефон сообщил: свернул, переключился или потерял связь — дисквалификация.
    /// </summary>
    public Task CheatAsync(Guid eventId, Guid userId) => UpsertAsync(eventId, userId, answer => answer.Disqualified = true);

    private async Task UpsertAsync(Guid eventId, Guid userId, Action<DaysAnswer> change)
    {
        await AnswerLock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.Days, tracking: false);
            var state = DaysModule.StateOf(ev);
            if (state.Phase != DaysPhase.Answering || time.GetUtcNow() >= state.Deadline)
            {
                throw new EventRuleException("too_late");
            }

            var answer = await db.DaysAnswers.FirstOrDefaultAsync(a => a.EventId == eventId && a.UserId == userId);
            if (answer is null)
            {
                answer = new DaysAnswer { Id = Guid.NewGuid(), EventId = eventId, UserId = userId };
                db.DaysAnswers.Add(answer);
            }

            change(answer);
            await db.SaveChangesAsync();
        }
        finally
        {
            AnswerLock.Release();
        }

        await live.BroadcastAsync();
    }
}
