using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Reaction;

/// <summary>Время реакции в миллисекундах.</summary>
public record ReactionAttemptRequest(int Ms);

/// <summary>
/// Маршруты реакции.
/// </summary>
public static class ReactionEndpoints
{
    /// <summary>
    /// <c>POST /api/reaction/{id}/attempt</c> и <c>/false-start</c> — игрок.
    /// </summary>
    public static IEndpointRouteBuilder MapReaction(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors().MapGroup("/api/reaction/{id:guid}").RequireAuthorization(AuthEndpoints.PlayerPolicy);

        routes.MapPost("/attempt", async (Guid id, ReactionAttemptRequest body, ClaimsPrincipal user, ReactionService reaction) =>
        {
            await reaction.AttemptAsync(id, user.UserId()!.Value, body.Ms);
            return Results.NoContent();
        });

        routes.MapPost("/false-start", async (Guid id, ClaimsPrincipal user, ReactionService reaction) =>
        {
            await reaction.FalseStartAsync(id, user.UserId()!.Value);
            return Results.NoContent();
        });

        return app;
    }
}
