using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Events;

/// <summary>
/// Общие проверки модулей и сервисов ивентов.
/// </summary>
public static class EventQueries
{
    /// <summary>
    /// Идущий ивент нужного типа; нет такого или уже не идёт — <c>not_active</c>.
    /// </summary>
    /// <param name="tracking"><c>false</c> — только прочитать состояние, без сохранения.</param>
    public static async Task<Event> ActiveOfTypeAsync(this AppDbContext db, Guid eventId, EventType type, bool tracking = true)
    {
        var events = tracking ? db.Events : db.Events.AsNoTracking();
        return await events.FirstOrDefaultAsync(e => e.Id == eventId && e.Type == type && e.Status == EventStatus.Active)
            ?? throw new EventRuleException("not_active");
    }

    /// <summary>
    /// Старт без игроков бессмысленен — <c>not_enough_players</c>.
    /// </summary>
    public static async Task EnsurePlayersAsync(this AppDbContext db)
    {
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Player))
        {
            throw new EventRuleException("not_enough_players");
        }
    }
}
