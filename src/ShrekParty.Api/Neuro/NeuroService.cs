using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;

namespace ShrekParty.Api.Neuro;

/// <summary>
/// Догадки игроков в нейрофото.
/// </summary>
public class NeuroService(AppDbContext db, NeuroCatalog catalog, ILiveNotifier live, TimeProvider time)
{
    // Два быстрых тапа по вариантам — два запроса разом; без блокировки оба вставят догадку.
    private static readonly SemaphoreSlim GuessLock = new(1, 1);

    /// <summary>
    /// Догадка по текущему раунду: человек и/или персонаж — что передано, то и меняется; можно передумать до конца времени.
    /// </summary>
    public async Task GuessAsync(Guid eventId, Guid userId, string? person, string? character)
    {
        if ((person is null && character is null)
            || (person is not null && !NeuroCatalog.Names.Contains(person))
            || (character is not null && !catalog.Characters.Contains(character)))
        {
            throw new BadInputException("bad_answer");
        }

        await GuessLock.WaitAsync();
        try
        {
            var ev = await db.ActiveOfTypeAsync(eventId, EventType.NeuroPhoto, tracking: false);
            var state = NeuroModule.StateOf(ev);
            if (state.Phase != NeuroPhase.Guessing || time.GetUtcNow() >= state.Deadline)
            {
                throw new EventRuleException("too_late");
            }

            var roundId = state.RoundIds[state.Index];
            var guess = await db.NeuroGuesses.FirstOrDefaultAsync(g => g.EventId == eventId && g.RoundId == roundId && g.UserId == userId);
            if (guess is null)
            {
                guess = new NeuroGuess { Id = Guid.NewGuid(), EventId = eventId, RoundId = roundId, UserId = userId };
                db.NeuroGuesses.Add(guess);
            }

            guess.Person = person ?? guess.Person;
            guess.Character = character ?? guess.Character;
            await db.SaveChangesAsync();
        }
        finally
        {
            GuessLock.Release();
        }

        await live.BroadcastAsync();
    }
}
