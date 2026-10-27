using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Days;

/// <summary>Ответ: сколько дней.</summary>
public record DaysAnswerRequest(int Days);

/// <summary>
/// Маршруты «Сколько мне дней».
/// </summary>
public static class DaysEndpoints
{
    /// <summary>
    /// <c>POST /api/days/{id}/answer</c> и <c>/cheat</c> — игрок.
    /// </summary>
    public static IEndpointRouteBuilder MapDays(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors().MapGroup("/api/days/{id:guid}").RequireAuthorization(AuthEndpoints.PlayerPolicy);

        routes.MapPost("/answer", async (Guid id, DaysAnswerRequest body, ClaimsPrincipal user, DaysService days) =>
        {
            await days.AnswerAsync(id, user.UserId()!.Value, body.Days);
            return Results.NoContent();
        });

        routes.MapPost("/cheat", async (Guid id, ClaimsPrincipal user, DaysService days) =>
        {
            await days.CheatAsync(id, user.UserId()!.Value);
            return Results.NoContent();
        });

        return app;
    }
}
