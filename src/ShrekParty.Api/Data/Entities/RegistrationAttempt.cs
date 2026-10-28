namespace ShrekParty.Api.Data.Entities;

/// <summary>
/// Попытка регистрации: от первого «простого» пароля до успешного сложного.
/// </summary>
public class RegistrationAttempt
{
    public Guid Id { get; set; }

    public required string Login { get; set; }

    public required string LoginNormalized { get; set; }

    public required string FirstPasswordHash { get; set; }

    public int FirstScore { get; set; }

    public double FirstGuessesLog10 { get; set; }

    /// <summary>Паттерны zxcvbn первого пароля, JSON-массив строк.</summary>
    public required string FirstPatternsJson { get; set; }

    /// <summary>Маска первого пароля — только для слабых.</summary>
    public string? FirstMask { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public double? SecondGuessesLog10 { get; set; }

    public Guid? UserId { get; set; }
}
