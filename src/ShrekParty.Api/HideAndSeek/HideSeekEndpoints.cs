using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.HideAndSeek;

/// <summary>Отметки на снимке.</summary>
public record HideMarksRequest(IReadOnlyList<Guid>? UserIds);

/// <summary>Голос за снимок.</summary>
public record HideVoteRequest(Guid ShotId);

/// <summary>
/// Маршруты пряток.
/// </summary>
public static class HideSeekEndpoints
{
    /// <summary>
    /// Снимки и отметки — админ; <c>/api/hide/{id}/vote</c> — игрок.
    /// </summary>
    public static IEndpointRouteBuilder MapHideSeek(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var admin = routes.MapGroup("/api/admin/hide").RequireAuthorization(AuthEndpoints.AdminPolicy);

        // multipart: file + userIds через запятую.
        admin.MapPost("/{id:guid}/shots", async (Guid id, HttpRequest request, ClaimsPrincipal user, HideSeekService hide) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                || !TryParseIds(form["userIds"].ToString(), out var userIds))
            {
                return Results.BadRequest(new { error = "bad_request" });
            }

            await using var stream = file.OpenReadStream();
            var shotId = await hide.AddShotAsync(id, userIds, stream, file.ContentType, user.UserId()!.Value);
            return Results.Created($"/api/admin/hide/shots/{shotId}", new { id = shotId });
        }).DisableAntiforgery();

        admin.MapPut("/shots/{shotId:guid}/players", async (Guid shotId, HideMarksRequest body, HideSeekService hide) =>
        {
            await hide.SetPlayersAsync(shotId, body.UserIds ?? []);
            return Results.NoContent();
        });

        admin.MapDelete("/shots/{shotId:guid}", async (Guid shotId, HideSeekService hide) =>
        {
            await hide.DeleteShotAsync(shotId);
            return Results.NoContent();
        });

        admin.MapPost("/{id:guid}/stop", async (Guid id, HideSeekService hide) =>
        {
            await hide.StopShootingAsync(id);
            return Results.NoContent();
        });

        routes.MapPost("/api/hide/{id:guid}/vote", async (Guid id, HideVoteRequest body, ClaimsPrincipal user, HideSeekService hide) =>
            {
                await hide.VoteAsync(id, body.ShotId, user.UserId()!.Value);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }

    private static bool TryParseIds(string raw, out List<Guid> ids)
    {
        ids = [];
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
            {
                return false;
            }

            ids.Add(id);
        }

        return true;
    }
}
