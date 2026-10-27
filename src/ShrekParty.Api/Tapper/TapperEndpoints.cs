using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Tapper;

/// <summary>Пачка тапов.</summary>
public record TapsRequest(int Count);

/// <summary>
/// Маршруты тапалки.
/// </summary>
public static class TapperEndpoints
{
    /// <summary>
    /// <c>POST /api/tapper/{id}/taps</c> — игрок.
    /// </summary>
    public static IEndpointRouteBuilder MapTapper(this IEndpointRouteBuilder app)
    {
        app.WithRuleErrors().MapPost("/api/tapper/{id:guid}/taps", async (Guid id, TapsRequest body, ClaimsPrincipal user, TapperService tapper) =>
            {
                await tapper.AddTapsAsync(id, user.UserId()!.Value, body.Count);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }
}
