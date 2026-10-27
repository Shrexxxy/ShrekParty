using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Tournament;

/// <summary>
/// Маршруты турнира: пульт матчей (админ) и решение про вызов (победитель финала).
/// </summary>
public static class TournamentEndpoints
{
    /// <summary>
    /// <c>/api/admin/matches/*</c> и <c>/api/tournaments/{id}/challenge</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapTournament(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin/matches").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapPost("/{id:guid}/start", (Guid id, TournamentService service) => Run(() => service.StartMatchAsync(id)));

        admin.MapPost("/{id:guid}/winner", (Guid id, MatchWinnerRequest request, TournamentService service) =>
            Run(() => service.SetWinnerAsync(id, request.WinnerId, request.Score)));

        admin.MapPost("/{id:guid}/undo", (Guid id, TournamentService service) => Run(() => service.UndoAsync(id)));

        admin.MapPost("/{id:guid}/photos", async (Guid id, IFormFile? file, ClaimsPrincipal user, TournamentService service) =>
        {
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            return await Run(() => service.AddPhotoAsync(id, stream, file.ContentType, user.UserId()!.Value));
        }).DisableAntiforgery();

        app.MapPost("/api/tournaments/{id:guid}/challenge", (Guid id, ChallengeRequest request, ClaimsPrincipal user, TournamentService service) =>
                Run(() => service.DecideChallengeAsync(id, user.UserId()!.Value, request.Accept)))
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }

    private static async Task<IResult> Run(Func<Task> action)
    {
        try
        {
            await action();
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
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }
}
