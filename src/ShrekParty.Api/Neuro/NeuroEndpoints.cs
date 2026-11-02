using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Neuro;

/// <summary>Догадка: кто на фото и/или под кого замаскирован — что передано, то и меняется.</summary>
public record NeuroGuessRequest(string? Person, string? Character);

/// <summary>
/// Маршруты нейрофото.
/// </summary>
public static class NeuroEndpoints
{
    /// <summary>
    /// Догадка <c>/api/neuro/{id}/guess</c> (игрок). Контент заготовлен заранее — загрузки нет.
    /// </summary>
    public static IEndpointRouteBuilder MapNeuro(this IEndpointRouteBuilder app)
    {
        app.WithRuleErrors().MapPost("/api/neuro/{id:guid}/guess", async (Guid id, NeuroGuessRequest body, ClaimsPrincipal user, NeuroService neuro) =>
            {
                await neuro.GuessAsync(id, user.UserId()!.Value, body.Person, body.Character);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }
}
