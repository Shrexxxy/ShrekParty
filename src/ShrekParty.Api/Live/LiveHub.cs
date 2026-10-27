using Microsoft.AspNetCore.SignalR;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Live;

/// <summary>
/// Real-time канал. Клиенты только слушают <c>Snapshot</c>; все действия идут через REST.
/// </summary>
public class LiveHub(ConnectionTracker connections, SnapshotBuilder snapshots, ILiveNotifier live) : Hub
{
    /// <summary>Имя метода клиента.</summary>
    public const string SnapshotMethod = "Snapshot";

    /// <summary>Группа неавторизованных гостей.</summary>
    public const string WaitingGroup = "waiting";

    /// <summary>Группа по роли.</summary>
    public static string RoleGroup(UserRole role) => role.ToString().ToLowerInvariant();

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var role = CurrentRole();
        if (role is null)
        {
            connections.AddWaiting(Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, WaitingGroup);
            // Счётчик ожидающих изменился — всем, включая нового.
            await live.BroadcastAsync();
            return;
        }

        var userId = Context.User!.UserId();
        if (role == UserRole.Player && userId is not null)
        {
            connections.AddPlayer(Context.ConnectionId, userId.Value);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RoleGroup(role.Value));
        var audience = new SnapshotAudience(
            SignedIn: true,
            ViewerId: role == UserRole.Player ? userId : null,
            IsAdmin: role == UserRole.Admin);
        await Clients.Caller.SendAsync(SnapshotMethod, await snapshots.BuildAsync(audience));
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (connections.Remove(Context.ConnectionId))
        {
            await live.BroadcastAsync();
        }
    }

    private UserRole? CurrentRole()
    {
        var user = Context.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return Enum.GetValues<UserRole>().Cast<UserRole?>().FirstOrDefault(r => user.IsInRole(r!.Value.ToString()));
    }
}
