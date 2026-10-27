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

        var players = connections.ConnectedPlayers.ToList();
        var snapshots = await builder.BuildManyAsync(
        [
            new SnapshotAudience(SignedIn: true, IsAdmin: true),
            new SnapshotAudience(SignedIn: true),
            SnapshotAudience.Guest,
            .. players.Select(p => new SnapshotAudience(SignedIn: true, ViewerId: p)),
        ]);

        await hub.Clients.Group(LiveHub.RoleGroup(UserRole.Admin)).SendAsync(LiveHub.SnapshotMethod, snapshots[0]);
        await hub.Clients.Group(LiveHub.RoleGroup(UserRole.Viewer)).SendAsync(LiveHub.SnapshotMethod, snapshots[1]);
        await hub.Clients.Group(LiveHub.WaitingGroup).SendAsync(LiveHub.SnapshotMethod, snapshots[2]);
        for (var i = 0; i < players.Count; i++)
        {
            await hub.Clients.User(players[i].ToString()).SendAsync(LiveHub.SnapshotMethod, snapshots[3 + i]);
        }
    }
}
