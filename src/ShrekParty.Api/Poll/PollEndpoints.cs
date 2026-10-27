using System.Security.Claims;
using ShrekParty.Api.Auth;

namespace ShrekParty.Api.Poll;

/// <summary>Голос.</summary>
public record VoteRequest(string? Option);

/// <summary>
/// Маршруты голосования за файтинг.
/// </summary>
public static class PollEndpoints
{
    /// <summary>
    /// <c>/api/events/{id}/vote</c> (игрок) и <c>/api/admin/poll/{id}/close</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapPoll(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/events/{id:guid}/vote", async (Guid id, VoteRequest request, ClaimsPrincipal user, FightPollModule poll) =>
                ToResult(await poll.VoteAsync(id, user.UserId()!.Value, request.Option)))
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        app.MapPost("/api/admin/poll/{id:guid}/close", async (Guid id, FightPollModule poll) => ToResult(await poll.CloseAsync(id)))
            .RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }

    private static IResult ToResult(PollOutcome outcome) => outcome switch
    {
        PollOutcome.Ok => Results.NoContent(),
        PollOutcome.NotFound => Results.NotFound(),
        PollOutcome.Closed => Results.Conflict(new { error = "closed" }),
        PollOutcome.UnknownOption => Results.BadRequest(new { error = "unknown_option" }),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
