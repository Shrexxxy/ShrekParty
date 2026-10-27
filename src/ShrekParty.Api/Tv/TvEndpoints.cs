using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Tv;

/// <summary>
/// Данные для экрана ТВ и управление его контентом.
/// </summary>
public static class TvEndpoints
{
    /// <summary>
    /// <c>/api/tv/*</c> (ТВ/админ) и <c>/api/admin/{tv,captions,memes}</c> (админ).
    /// </summary>
    public static IEndpointRouteBuilder MapTv(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tv/facts", (TvFactsService facts) => facts.GetFactsAsync())
            .RequireAuthorization(AuthEndpoints.ViewerPolicy);

        app.MapGet("/api/tv/slides", (TvSlidesService slides) => slides.GetSlidesAsync())
            .RequireAuthorization(AuthEndpoints.ViewerPolicy);

        var admin = app.MapGroup("/api/admin").RequireAuthorization(AuthEndpoints.AdminPolicy);

        // multipart: text, seconds, необязательно file.
        admin.MapPost("/tv/message", async (HttpRequest request, ClaimsPrincipal user, TvContentService tv) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is not null && (file.Length > AvatarEndpoints.MaxUploadBytes || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            int.TryParse(form["seconds"], out var seconds);
            try
            {
                await using var stream = file?.OpenReadStream();
                await tv.PostMessageAsync(form["text"], seconds, stream, file?.ContentType, user.UserId()!.Value);
                return Results.NoContent();
            }
            catch (BadInputException e)
            {
                return Results.BadRequest(new { error = e.Code });
            }
        }).DisableAntiforgery();

        admin.MapDelete("/tv/message", async (TvContentService tv) =>
        {
            await tv.ClearMessageAsync();
            return Results.NoContent();
        });

        admin.MapGet("/captions", (TvContentService tv) => tv.GetCaptionsAsync());

        admin.MapPost("/captions", async (CaptionRequest request, TvContentService tv) =>
        {
            try
            {
                var caption = await tv.AddCaptionAsync(request.Text);
                return Results.Created($"/api/admin/captions/{caption.Id}", caption);
            }
            catch (BadInputException e)
            {
                return Results.BadRequest(new { error = e.Code });
            }
        });

        admin.MapDelete("/captions/{id:guid}", async (Guid id, TvContentService tv) =>
            await tv.DeleteCaptionAsync(id) ? Results.NoContent() : Results.NotFound());

        admin.MapGet("/memes", (TvContentService tv) => tv.GetMemesAsync());

        // multipart: file, необязательно caption.
        admin.MapPost("/memes", async (HttpRequest request, ClaimsPrincipal user, TvContentService tv) =>
        {
            var form = await request.ReadFormAsync();
            var file = form.Files["file"];
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes
                || !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            await tv.AddMemeAsync(stream, file.ContentType, form["caption"], user.UserId()!.Value);
            return Results.NoContent();
        }).DisableAntiforgery();

        admin.MapDelete("/memes/{id:guid}", async (Guid id, TvContentService tv) =>
            await tv.DeleteMemeAsync(id) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
