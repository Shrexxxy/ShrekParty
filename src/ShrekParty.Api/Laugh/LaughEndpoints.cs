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
        app.MapPut("/api/admin/laugh/{id:guid}/scores", async (Guid id, LaughScoreRequest request, LaughModule laugh) =>
        {
            try
            {
                await laugh.SetScoreAsync(id, request);
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
        }).RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }
}
