using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.Quiz;
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
/// <param name="TvPreload">Сколько медиа Своей игры скачало ТВ — только админу.</param>
public record AppSnapshot(
    RegistrationStatusDto Registration,
    IReadOnlyList<LeaderboardRowDto> Leaderboard,
    DateTimeOffset ServerTime,
    ActiveEventDto? ActiveEvent = null,
    TvMessageDto? TvMessage = null,
    TvPreloadDto? TvPreload = null);

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
public class SnapshotBuilder(
    RegistrationService registration,
    DonkeyService donkeys,
    EventEngine events,
    TvContentService tv,
    TvPreloadStatus preload,
    TimeProvider time)
{
    /// <summary>
    /// Собрать снапшот для зрителя.
    /// </summary>
    public async Task<AppSnapshot> BuildAsync(SnapshotAudience audience) => (await BuildManyAsync([audience]))[0];

    /// <summary>
    /// Снапшоты для нескольких зрителей разом: общее (регистрация, таблица, сообщение ТВ) читается один раз,
    /// для каждого — только детали ивента. Порядок — как у <paramref name="audiences"/>.
    /// </summary>
    public async Task<IReadOnlyList<AppSnapshot>> BuildManyAsync(IReadOnlyList<SnapshotAudience> audiences)
    {
        var status = await registration.GetStatusAsync();
        var now = time.GetUtcNow();
        var guest = new AppSnapshot(status, [], now);
        if (!audiences.Any(a => a.SignedIn))
        {
            return audiences.Select(_ => guest).ToList();
        }

        var leaderboard = await donkeys.GetLeaderboardAsync();
        var message = await tv.GetActiveMessageAsync();
        var result = new List<AppSnapshot>(audiences.Count);
        foreach (var audience in audiences)
        {
            result.Add(audience.SignedIn
                ? new AppSnapshot(
                    status, leaderboard, now, await events.GetActiveAsync(audience.ViewerId, audience.IsAdmin), message,
                    audience.IsAdmin ? preload.Current : null)
                : guest);
        }

        return result;
    }
}
