using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Tote;

/// <summary>Ставка: на кого и сколько.</summary>
public record PlaceBetRequest(Guid OnPlayerId, int Amount);

/// <summary>
/// Маршруты тотализатора.
/// </summary>
public static class ToteEndpoints
{
    /// <summary>
    /// <c>/api/matches/{id}/bets</c> — ставка игрока.
    /// </summary>
    public static IEndpointRouteBuilder MapTote(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/matches/{id:guid}/bets", async (Guid id, PlaceBetRequest request, ClaimsPrincipal user, ToteService tote) =>
        {
            try
            {
                await tote.PlaceBetAsync(id, user.UserId()!.Value, request.OnPlayerId, request.Amount);
                return Results.NoContent();
            }
            catch (EventRuleException e)
            {
                return Results.Conflict(new { error = e.Code });
            }
            catch (ArgumentException e)
            {
                return Results.BadRequest(new { error = e.Message });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound();
            }
        }).RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }
}
