namespace ShrekParty.Api.Scoring;

/// <summary>
/// Схема перевода места в ослики.
/// </summary>
public enum ScoringScheme
{
    /// <summary>Равный шаг от максимума до максимум/N.</summary>
    Linear,

    /// <summary>Упор на призёров: 100% / 70% / 50%, дальше лесенкой от 40%.</summary>
    Podium,

    /// <summary>Ручная таблица «место → ослики».</summary>
    Custom,
}
