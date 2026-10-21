using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Registration;

/// <summary>
/// Состояние регистрации.
/// </summary>
public enum RegistrationState
{
    /// <summary>Ещё не открыта — гости ждут.</summary>
    Closed,

    /// <summary>Открыта.</summary>
    Open,

    /// <summary>Закрыта админом, очки начислены; опоздавшие ещё могут зарегистрироваться.</summary>
    Finished,
}

/// <summary>
/// Статус регистрации для клиентов.
/// </summary>
public record RegistrationStatusDto(RegistrationState State, DateTimeOffset? OpenedAt, int WaitingCount, DateTimeOffset? ClosedAt = null);

/// <summary>
/// Строка таблицы регистрации: за что дали ослики.
/// </summary>
/// <param name="Place">Место по итогу регистрации; у опоздавших — null.</param>
/// <param name="SpeedPlace">Место по скорости — для медалек; у опоздавших — null.</param>
/// <param name="ElapsedSeconds">От открытия регистрации до успешного завершения.</param>
/// <param name="PasswordScore">Оценка второго пароля 0–4.</param>
/// <param name="PasswordGuessesLog10">log10 попыток подбора — для «взломают за …».</param>
/// <param name="IsLate">Зарегистрировался после закрытия — без мест и очков, в конце таблицы.</param>
public record RegistrationResultRowDto(
    Guid UserId,
    string Login,
    string? AvatarUrl,
    int? Place,
    int? SpeedPlace,
    double ElapsedSeconds,
    int PasswordScore,
    double PasswordGuessesLog10,
    int SpeedDonkeys,
    int PasswordDonkeys,
    int TotalDonkeys,
    bool IsLate = false);

/// <summary>
/// Закрытие регистрации: схема очков (по умолчанию — линейная).
/// </summary>
public record CloseRegistrationRequest(ScoringScheme Scheme = ScoringScheme.Linear, Dictionary<int, int>? CustomTable = null);

/// <summary>
/// Шаг 1: логин и первый пароль.
/// </summary>
public record StartRegistrationRequest(string Login, string Password);

/// <summary>
/// Ответ шага 1.
/// </summary>
public record StartRegistrationResponse(Guid AttemptId);

/// <summary>
/// Шаг 2: «сложный» пароль.
/// </summary>
public record CompleteRegistrationRequest(Guid AttemptId, string NewPassword);
