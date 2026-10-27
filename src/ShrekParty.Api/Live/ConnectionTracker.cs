using System.Collections.Concurrent;

namespace ShrekParty.Api.Live;

/// <summary>
/// Учёт подключений: гости, ждущие открытия регистрации, и игроки — им снапшот шлётся персонально.
/// </summary>
public class ConnectionTracker
{
    private readonly ConcurrentDictionary<string, byte> _waiting = new();
    private readonly ConcurrentDictionary<string, Guid> _players = new();

    /// <summary>Сколько гостей сейчас ждёт.</summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>Игроки, у которых есть хотя бы одно подключение.</summary>
    public IReadOnlyCollection<Guid> ConnectedPlayers => _players.Values.Distinct().ToList();

    /// <summary>Учесть ждущее подключение.</summary>
    public void AddWaiting(string connectionId) => _waiting.TryAdd(connectionId, 0);

    /// <summary>Учесть подключение игрока.</summary>
    public void AddPlayer(string connectionId, Guid userId) => _players[connectionId] = userId;

    /// <summary>Снять подключение с учёта; <c>true</c>, если оно было ждущим (счётчик изменился).</summary>
    public bool Remove(string connectionId)
    {
        _players.TryRemove(connectionId, out _);
        return _waiting.TryRemove(connectionId, out _);
    }
}
