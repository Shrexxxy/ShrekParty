namespace ShrekParty.Api.Events;

/// <summary>
/// Нарушено правило ивента (мало игроков, турнир не доигран…). Эндпоинты отвечают 409 с кодом.
/// </summary>
/// <param name="code">Код для клиента: <c>{ error: code }</c>.</param>
public class EventRuleException(string code) : Exception(code)
{
    /// <summary>Код ошибки для клиента.</summary>
    public string Code { get; } = code;
}
