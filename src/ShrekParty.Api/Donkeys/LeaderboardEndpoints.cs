using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Donkeys;

/// <summary>
/// Таблица лидеров и список ивентов для вкладок.
/// </summary>
public static class LeaderboardEndpoints
{
    /// <summary>
    /// Маршруты <c>/api/leaderboard</c> и <c>/api/events</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapLeaderboard(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/leaderboard", (Guid? eventId, DonkeyService donkeys) => donkeys.GetLeaderboardAsync(eventId))
            .RequireAuthorization();

        app.MapGet("/api/events", async (AppDbContext db) =>
        {
            var events = await db.Events.AsNoTracking()
                .Where(e => e.Status == EventStatus.Active || e.Status == EventStatus.Finished)
                // Вкладка нужна только тем, кто даёт ослики.
                .Where(e => e.Type != EventType.Ceremony)
                .ToListAsync();

            return events
                .OrderBy(e => e.StartedAt)
                .Select(e => new EventDto(e.Id, e.Type, e.Title, e.Status, e.MaxDonkeys));
        }).RequireAuthorization();

        return app;
    }
}
