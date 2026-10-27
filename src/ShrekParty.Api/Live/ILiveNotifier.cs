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
/// Рассылка через SignalR: админу и ТВ — по группам, каждому игроку — свой снапшот (детали ивента у всех разные),
/// гостям — без таблицы и ивента.
/// </summary>
public class LiveNotifier(IHubContext<LiveHub> hub, IServiceScopeFactory scopes, ConnectionTracker connections) : ILiveNotifier
{
    /// <inheritdoc/>
    public async Task BroadcastAsync()
    {
        // Свой scope: вызывают из разных мест, а читать нужно уже сохранённое.
        using var scope = scopes.CreateScope();
        var builder = scope.ServiceProvider.GetRequiredService<SnapshotBuilder>();

        await hub.Clients.Group(LiveHub.RoleGroup(UserRole.Admin))
            .SendAsync(LiveHub.SnapshotMethod, await builder.BuildAsync(new SnapshotAudience(SignedIn: true, IsAdmin: true)));
        await hub.Clients.Group(LiveHub.RoleGroup(UserRole.Viewer))
            .SendAsync(LiveHub.SnapshotMethod, await builder.BuildAsync(new SnapshotAudience(SignedIn: true)));
        foreach (var playerId in connections.ConnectedPlayers)
        {
            await hub.Clients.User(playerId.ToString())
                .SendAsync(LiveHub.SnapshotMethod, await builder.BuildAsync(new SnapshotAudience(SignedIn: true, ViewerId: playerId)));
        }

        await hub.Clients.Group(LiveHub.WaitingGroup).SendAsync(LiveHub.SnapshotMethod, await builder.BuildAsync(SnapshotAudience.Guest));
    }
}
