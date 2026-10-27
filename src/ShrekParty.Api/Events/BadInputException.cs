namespace ShrekParty.Api.Events;

/// <summary>
/// Неверные данные от клиента (отрицательная ставка, чужой игрок в отметках…). Эндпоинты отвечают 400 с кодом.
/// Отдельный тип, а не <see cref="ArgumentException"/>: ошибки в собственном коде не должны маскироваться под 400.
/// </summary>
/// <param name="code">Код для клиента: <c>{ error: code }</c>.</param>
public class BadInputException(string code) : Exception(code)
{
    /// <summary>Код ошибки для клиента.</summary>
    public string Code { get; } = code;
}
