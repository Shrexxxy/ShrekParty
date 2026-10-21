using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Registration;

namespace ShrekParty.Api.Live;

/// <summary>
/// Полное состояние приложения для клиента.
/// </summary>
/// <param name="Registration">Статус регистрации и счётчик ожидающих.</param>
/// <param name="Leaderboard">Общая таблица; для гостей без входа — пустая.</param>
/// <param name="ServerTime">Время сборки — клиент отбрасывает снапшоты старее уже показанного.</param>
public record AppSnapshot(
    RegistrationStatusDto Registration,
    IReadOnlyList<LeaderboardRowDto> Leaderboard,
    DateTimeOffset ServerTime);

/// <summary>
/// Собирает снапшот из текущего состояния.
/// </summary>
public class SnapshotBuilder(RegistrationService registration, DonkeyService donkeys, TimeProvider time)
{
    /// <summary>
    /// Собрать снапшот.
    /// </summary>
    /// <param name="includeLeaderboard"><c>false</c> — для неавторизованных гостей.</param>
    public async Task<AppSnapshot> BuildAsync(bool includeLeaderboard)
    {
        var status = await registration.GetStatusAsync();
        var leaderboard = includeLeaderboard ? await donkeys.GetLeaderboardAsync() : [];
        return new AppSnapshot(status, leaderboard, time.GetUtcNow());
    }
}
