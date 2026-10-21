using System.Collections.Concurrent;

namespace ShrekParty.Api.Live;

/// <summary>
/// Учёт анонимных подключений — гостей, ждущих открытия регистрации.
/// </summary>
public class ConnectionTracker
{
    private readonly ConcurrentDictionary<string, byte> _waiting = new();

    /// <summary>Сколько гостей сейчас ждёт.</summary>
    public int WaitingCount => _waiting.Count;

    /// <summary>Учесть ждущее подключение.</summary>
    public void AddWaiting(string connectionId) => _waiting.TryAdd(connectionId, 0);

    /// <summary>Снять подключение с учёта; <c>true</c>, если оно было ждущим.</summary>
    public bool Remove(string connectionId) => _waiting.TryRemove(connectionId, out _);
}
