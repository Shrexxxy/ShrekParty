using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Tournament;

/// <summary>
/// Маршруты турнира: пульт матчей (админ), фото матча (любой гость, пока матч идёт) и решение про вызов (победитель финала).
/// </summary>
public static class TournamentEndpoints
{
    /// <summary>
    /// <c>/api/admin/matches/*</c> и <c>/api/tournaments/{id}/challenge</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapTournament(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var admin = routes.MapGroup("/api/admin/matches").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapPost("/{id:guid}/start", async (Guid id, TournamentService service) =>
        {
            await service.StartMatchAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/{id:guid}/winner", async (Guid id, MatchWinnerRequest request, TournamentService service) =>
        {
            await service.SetWinnerAsync(id, request.WinnerId, request.Score);
            return Results.NoContent();
        });

        admin.MapPost("/{id:guid}/undo", async (Guid id, TournamentService service) =>
        {
            await service.UndoAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/{id:guid}/photos", (Guid id, IFormFile? file, ClaimsPrincipal user, TournamentService service) =>
            AddPhotoAsync(id, file, user, service, onlyLive: false)).DisableAntiforgery();

        // Снимать матч может любой гость — пока матч идёт.
        routes.MapPost("/api/matches/{id:guid}/photos", (Guid id, IFormFile? file, ClaimsPrincipal user, TournamentService service) =>
                AddPhotoAsync(id, file, user, service, onlyLive: true))
            .RequireAuthorization(AuthEndpoints.PlayerPolicy)
            .DisableAntiforgery();

        routes.MapPost("/api/tournaments/{id:guid}/challenge", async (Guid id, ChallengeRequest request, ClaimsPrincipal user, TournamentService service) =>
            {
                await service.DecideChallengeAsync(id, user.UserId()!.Value, request.Accept);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }

    private static async Task<IResult> AddPhotoAsync(Guid id, IFormFile? file, ClaimsPrincipal user, TournamentService service, bool onlyLive)
    {
        if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
            || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { error = "bad_file" });
        }

        await using var stream = file.OpenReadStream();
        await service.AddPhotoAsync(id, stream, file.ContentType, user.UserId()!.Value, onlyLive);
        return Results.NoContent();
    }
}
