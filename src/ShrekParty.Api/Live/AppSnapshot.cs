using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.Registration;
using ShrekParty.Api.Tv;

namespace ShrekParty.Api.Live;

/// <summary>
/// Полное состояние приложения для клиента.
/// </summary>
/// <param name="Registration">Статус регистрации и счётчик ожидающих.</param>
/// <param name="Leaderboard">Общая таблица; для гостей без входа — пустая.</param>
/// <param name="ServerTime">Время сборки — клиент отбрасывает снапшоты старее уже показанного.</param>
/// <param name="ActiveEvent">Текущий ивент программы с деталями для этого зрителя; гостям — null.</param>
/// <param name="TvMessage">Ручное сообщение именинника на ТВ, пока не истекло.</param>
public record AppSnapshot(
    RegistrationStatusDto Registration,
    IReadOnlyList<LeaderboardRowDto> Leaderboard,
    DateTimeOffset ServerTime,
    ActiveEventDto? ActiveEvent = null,
    TvMessageDto? TvMessage = null);

/// <summary>
/// Кому собирается снапшот.
/// </summary>
/// <param name="SignedIn">Вошёл ли зритель — гостям без входа таблица и ивент не нужны.</param>
/// <param name="ViewerId">Игрок; null — ТВ или админ.</param>
public record SnapshotAudience(bool SignedIn, Guid? ViewerId = null, bool IsAdmin = false)
{
    /// <summary>Гость без входа.</summary>
    public static readonly SnapshotAudience Guest = new(SignedIn: false);
}

/// <summary>
/// Собирает снапшот из текущего состояния.
/// </summary>
public class SnapshotBuilder(RegistrationService registration, DonkeyService donkeys, EventEngine events, TvContentService tv, TimeProvider time)
{
    /// <summary>
    /// Собрать снапшот для зрителя.
    /// </summary>
    public async Task<AppSnapshot> BuildAsync(SnapshotAudience audience)
    {
        var status = await registration.GetStatusAsync();
        if (!audience.SignedIn)
        {
            return new AppSnapshot(status, [], time.GetUtcNow());
        }

        var leaderboard = await donkeys.GetLeaderboardAsync();
        var active = await events.GetActiveAsync(audience.ViewerId, audience.IsAdmin);
        return new AppSnapshot(status, leaderboard, time.GetUtcNow(), active, await tv.GetActiveMessageAsync());
    }
}
