using Microsoft.AspNetCore.SignalR;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Live;

/// <summary>
/// Рассылает клиентам актуальное состояние после любых изменений.
/// </summary>
public interface ILiveNotifier
{
    /// <summary>Разослать свежий снапшот всем подключённым.</summary>
    Task BroadcastAsync();
}

/// <summary>
/// Рассылка через SignalR: вошедшим — полный снапшот, гостям — без таблицы лидеров.
/// </summary>
public class LiveNotifier(IHubContext<LiveHub> hub, IServiceScopeFactory scopes) : ILiveNotifier
{
    /// <inheritdoc/>
    public async Task BroadcastAsync()
    {
        // Свой scope: вызывают из разных мест, а читать нужно уже сохранённое.
        using var scope = scopes.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<SnapshotBuilder>();

        var full = await builder.BuildAsync(includeLeaderboard: true);
        var guest = full with { Leaderboard = [] };
        var signedIn = Enum.GetValues<UserRole>().Select(LiveHub.RoleGroup).ToList();

        await hub.Clients.Groups(signedIn).SendAsync(LiveHub.SnapshotMethod, full);
        await hub.Clients.Group(LiveHub.WaitingGroup).SendAsync(LiveHub.SnapshotMethod, guest);
    }
}
