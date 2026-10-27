using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Laugh;

/// <summary>
/// Ввод очков Смехлыста в админке.
/// </summary>
public static class LaughEndpoints
{
    /// <summary>
    /// <c>PUT /api/admin/laugh/{id}/scores</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapLaugh(this IEndpointRouteBuilder app)
    {
        app.WithRuleErrors().MapPut("/api/admin/laugh/{id:guid}/scores", async (Guid id, LaughScoreRequest request, LaughModule laugh) =>
        {
            await laugh.SetScoreAsync(id, request);
            return Results.NoContent();
        }).RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }
}
