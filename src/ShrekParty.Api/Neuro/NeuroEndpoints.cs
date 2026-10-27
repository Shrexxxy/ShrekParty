using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Neuro;

/// <summary>Кто на фото.</summary>
public record NeuroMarkRequest(Guid? SubjectId);

/// <summary>Догадка: кто на фото.</summary>
public record NeuroGuessRequest(Guid UserId);

/// <summary>
/// Маршруты нейрофото.
/// </summary>
public static class NeuroEndpoints
{
    /// <summary>
    /// Контент <c>/api/admin/neuro/photos</c> (админ) и догадка <c>/api/neuro/{id}/guess</c> (игрок).
    /// </summary>
    public static IEndpointRouteBuilder MapNeuro(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var admin = routes.MapGroup("/api/admin/neuro/photos").RequireAuthorization(AuthEndpoints.AdminPolicy);

        admin.MapGet("/", (NeuroService neuro) => neuro.GetPhotosAsync());

        admin.MapPost("/", async (IFormFile? file, ClaimsPrincipal user, NeuroService neuro) =>
        {
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            var id = await neuro.UploadAsync(stream, file.ContentType, user.UserId()!.Value);
            return Results.Created($"/api/admin/neuro/photos/{id}", new { id });
        }).DisableAntiforgery();

        admin.MapPut("/{photoId:guid}", async (Guid photoId, NeuroMarkRequest body, NeuroService neuro) =>
        {
            await neuro.MarkAsync(photoId, body.SubjectId);
            return Results.NoContent();
        });

        admin.MapDelete("/{photoId:guid}", async (Guid photoId, NeuroService neuro) =>
        {
            await neuro.DeleteAsync(photoId);
            return Results.NoContent();
        });

        routes.MapPost("/api/neuro/{id:guid}/guess", async (Guid id, NeuroGuessRequest body, ClaimsPrincipal user, NeuroService neuro) =>
            {
                await neuro.GuessAsync(id, user.UserId()!.Value, body.UserId);
                return Results.NoContent();
            })
            .RequireAuthorization(AuthEndpoints.PlayerPolicy);

        return app;
    }
}
