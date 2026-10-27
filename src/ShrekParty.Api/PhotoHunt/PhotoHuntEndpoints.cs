using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.PhotoHunt;

/// <summary>
/// Маршруты фотоохоты.
/// </summary>
public static class PhotoHuntEndpoints
{
    /// <summary>
    /// <c>POST /api/photo/{id}</c> (игрок, multipart file) и лайки <c>PUT/DELETE /api/photo/{id}/likes/{ownerId}</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapPhotoHunt(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/photo/{id:guid}", async (Guid id, IFormFile? file, ClaimsPrincipal user, PhotoHuntService hunt) =>
        {
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            return await Run(() => hunt.SubmitAsync(id, user.UserId()!.Value, stream, file.ContentType));
        }).RequireAuthorization(AuthEndpoints.PlayerPolicy).DisableAntiforgery();

        app.MapPut("/api/photo/{id:guid}/likes/{ownerId:guid}", (Guid id, Guid ownerId, ClaimsPrincipal user, PhotoHuntService hunt) =>
                Run(() => hunt.LikeAsync(id, user.UserId()!.Value, ownerId)))
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        app.MapDelete("/api/photo/{id:guid}/likes/{ownerId:guid}", (Guid id, Guid ownerId, ClaimsPrincipal user, PhotoHuntService hunt) =>
                Run(() => hunt.UnlikeAsync(id, user.UserId()!.Value, ownerId)))
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
    }
}
