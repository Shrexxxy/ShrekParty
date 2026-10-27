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
        var routes = app.WithRuleErrors();

        routes.MapPost("/api/photo/{id:guid}", async (Guid id, IFormFile? file, ClaimsPrincipal user, PhotoHuntService hunt) =>
        {
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            await hunt.SubmitAsync(id, user.UserId()!.Value, stream, file.ContentType);
            return Results.NoContent();
        }).RequireAuthorization(AuthEndpoints.PlayerPolicy).DisableAntiforgery();

        routes.MapPut("/api/photo/{id:guid}/likes/{ownerId:guid}", async (Guid id, Guid ownerId, ClaimsPrincipal user, PhotoHuntService hunt) =>
            {
                await hunt.LikeAsync(id, user.UserId()!.Value, ownerId);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        routes.MapDelete("/api/photo/{id:guid}/likes/{ownerId:guid}", async (Guid id, Guid ownerId, ClaimsPrincipal user, PhotoHuntService hunt) =>
            {
                await hunt.UnlikeAsync(id, user.UserId()!.Value, ownerId);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }
}
