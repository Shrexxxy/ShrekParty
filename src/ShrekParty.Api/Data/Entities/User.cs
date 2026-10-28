namespace ShrekParty.Api.Data.Entities;

/// <summary>
/// Роль пользователя.
/// </summary>
public enum UserRole
{
    /// <summary>Гость-участник.</summary>
    Player,

    /// <summary>Именинник, управляет всем.</summary>
    Admin,

    /// <summary>Табло на ТВ, только показывает.</summary>
    Viewer,
}

/// <summary>
/// Пользователь приложения.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public required string Login { get; set; }

    /// <summary>Логин в верхнем регистре — для уникальности без учёта регистра.</summary>
    public required string LoginNormalized { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; }

    public Guid? AvatarMediaId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Нормализует логин для сравнения.</summary>
    public static string Normalize(string login) => login.Trim().ToUpperInvariant();
}
