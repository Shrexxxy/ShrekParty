namespace ShrekParty.Api.Events;

/// <summary>Тело ошибки для клиента: <c>{ error: code }</c>.</summary>
public record ErrorBody(string Error);

/// <summary>
/// Ошибки действий в ивентах → коды ответа, одним местом для всех маршрутов ивентов:
/// правило нарушено — 409 с кодом, неверные данные — 400, нет такого — 404, чужое действие — 403.
/// Остальное — ошибка сервера и уходит в лог как есть.
/// </summary>
public class RuleErrorFilter : IEndpointFilter
{
    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (EventRuleException e)
        {
            return TypedResults.Conflict(new ErrorBody(e.Code));
        }
        catch (BadInputException e)
        {
            return TypedResults.BadRequest(new ErrorBody(e.Code));
        }
        catch (KeyNotFoundException)
        {
            return TypedResults.NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Forbid();
        }
    }
}

/// <summary>
/// Подключение <see cref="RuleErrorFilter"/>.
/// </summary>
public static class RuleErrorFilterExtensions
{
    /// <summary>
    /// Группа без префикса: маршруты внутри получают общий перевод ошибок в коды ответа.
    /// </summary>
    public static RouteGroupBuilder WithRuleErrors(this IEndpointRouteBuilder app) =>
        app.MapGroup("").AddEndpointFilter<RuleErrorFilter>();
}
