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
        var admin = app.MapGroup("/api/admin/hide").RequireAuthorization(AuthEndpoints.AdminPolicy);

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
            return await Run(async () =>
            {
                var shotId = await hide.AddShotAsync(id, userIds, stream, file.ContentType, user.UserId()!.Value);
                return Results.Created($"/api/admin/hide/shots/{shotId}", new { id = shotId });
            });
        }).DisableAntiforgery();

        admin.MapPut("/shots/{shotId:guid}/players", (Guid shotId, HideMarksRequest body, HideSeekService hide) =>
            Run(() => hide.SetPlayersAsync(shotId, body.UserIds ?? [])));

        admin.MapDelete("/shots/{shotId:guid}", (Guid shotId, HideSeekService hide) =>
            Run(() => hide.DeleteShotAsync(shotId)));

        admin.MapPost("/{id:guid}/stop", (Guid id, HideSeekService hide) =>
            Run(() => hide.StopShootingAsync(id)));

        app.MapPost("/api/hide/{id:guid}/vote", (Guid id, HideVoteRequest body, ClaimsPrincipal user, HideSeekService hide) =>
                Run(() => hide.VoteAsync(id, body.ShotId, user.UserId()!.Value)))
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

    private static Task<IResult> Run(Func<Task> action) => Run(async () =>
    {
        await action();
        return Results.NoContent();
    });

    private static async Task<IResult> Run(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
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
    }
}
