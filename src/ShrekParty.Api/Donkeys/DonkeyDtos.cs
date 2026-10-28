using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Donkeys;

/// <summary>
/// Строка таблицы лидеров.
/// </summary>
/// <param name="Cash">«На руках»: ослики минус покупки на аукционе; места считаются по <paramref name="Donkeys"/>.</param>
public record LeaderboardRowDto(Guid UserId, string Login, string? AvatarUrl, int Donkeys, int Place, int Cash);

/// <summary>
/// Ивент для вкладок таблицы.
/// </summary>
public record EventDto(Guid Id, EventType Type, string Title, EventStatus Status, int MaxDonkeys);

/// <summary>
/// Ручное начисление от админа.
/// </summary>
public record ManualAwardRequest(Guid UserId, int Amount, string Reason);

/// <summary>
/// Причины начислений.
/// </summary>
public static class DonkeyReasons
{
    public const string StartCapital = "Стартовый капитал";
    public const string RegistrationSpeed = "Регистрация: скорость";
    public const string RegistrationPassword = "Регистрация: пароль";
    public const string Avatar = "Аватарка";
}
