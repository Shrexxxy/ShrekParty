using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Ceremony;

/// <summary>
/// Пульт церемонии.
/// </summary>
public static class CeremonyEndpoints
{
    /// <summary>
    /// <c>POST /api/admin/ceremony/{id}/next</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapCeremony(this IEndpointRouteBuilder app)
    {
        app.WithRuleErrors().MapPost("/api/admin/ceremony/{id:guid}/next", async (Guid id, CeremonyModule ceremony) =>
            {
                await ceremony.NextAsync(id);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.AdminPolicy);

        return app;
    }
}
