using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Events;

/// <summary>
/// Программа вечера в админке.
/// </summary>
public static class EventEndpoints
{
    /// <summary>
    /// Маршруты <c>/api/admin/events</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapEvents(this IEndpointRouteBuilder app)
    {
        // То же, что приходит в снапшоте, — для перезагрузки страницы и тестов.
        app.MapGet("/api/events/active", async (ClaimsPrincipal user, EventEngine engine) =>
        {
            var isPlayer = user.IsInRole(nameof(UserRole.Player));
            var active = await engine.GetActiveAsync(isPlayer ? user.UserId() : null, user.IsInRole(nameof(UserRole.Admin)));
            return active is null ? Results.NoContent() : Results.Ok(active);
        }).RequireAuthorization();

        var group = app.WithRuleErrors().MapGroup("/api/admin/events").RequireAuthorization(AuthEndpoints.AdminPolicy);

        group.MapGet("/", (EventEngine engine) => engine.GetProgramAsync());

        group.MapPost("/", async (CreateEventRequest request, EventEngine engine) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new { error = "title" });
            }

            var (outcome, item) = await engine.CreateAsync(request);
            return outcome == EngineOutcome.Ok
                ? Results.Created($"/api/admin/events/{item!.Id}", item)
                : ToResult(outcome);
        });

        group.MapPut("/{id:guid}/order", async (Guid id, ReorderRequest request, EventEngine engine) =>
            ToResult(await engine.ReorderAsync(id, request.Order)));

        group.MapDelete("/{id:guid}", async (Guid id, EventEngine engine) => ToResult(await engine.DeleteAsync(id)));

        group.MapPost("/{id:guid}/start", async (Guid id, EventEngine engine) => ToResult(await engine.StartAsync(id)));

        group.MapPost("/{id:guid}/finish", async (Guid id, EventEngine engine) => ToResult(await engine.FinishAsync(id)));

        return app;
    }

    private static IResult ToResult(EngineOutcome outcome) => outcome switch
    {
        EngineOutcome.Ok => Results.NoContent(),
        EngineOutcome.NotFound => Results.NotFound(),
        EngineOutcome.Unsupported => Results.BadRequest(new { error = "unsupported_type" }),
        EngineOutcome.AlreadyActive => Results.Conflict(new { error = "already_active" }),
        EngineOutcome.NotActive => Results.Conflict(new { error = "not_active" }),
        EngineOutcome.NotDraft => Results.Conflict(new { error = "not_draft" }),
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };
}
