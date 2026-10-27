using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Quiz;

/// <summary>
/// Редактор контента Своей игры в админке.
/// </summary>
public static class QuizContentEndpoints
{
    /// <summary>
    /// <c>/api/admin/quiz</c>: дерево, темы, вопросы, медиа, импорт/экспорт.
    /// </summary>
    public static IEndpointRouteBuilder MapQuizContent(this IEndpointRouteBuilder app)
    {
        var quiz = app.WithRuleErrors().MapGroup("/api/admin/quiz").RequireAuthorization(AuthEndpoints.AdminPolicy);

        quiz.MapGet("/", (QuizContentService content) => content.GetTreeAsync());

        quiz.MapPost("/themes", async (QuizThemeRequest request, QuizContentService content) =>
        {
            var id = await content.AddThemeAsync(request);
            return Results.Created($"/api/admin/quiz/themes/{id}", new { id });
        });

        quiz.MapPut("/themes/{id:guid}", async (Guid id, QuizThemeRenameRequest request, QuizContentService content) =>
        {
            await content.RenameThemeAsync(id, request);
            return Results.NoContent();
        });

        quiz.MapDelete("/themes/{id:guid}", async (Guid id, QuizContentService content) =>
        {
            await content.DeleteThemeAsync(id);
            return Results.NoContent();
        });

        quiz.MapPost("/themes/{id:guid}/questions", async (Guid id, QuizQuestionRequest request, QuizContentService content) =>
        {
            var questionId = await content.AddQuestionAsync(id, request);
            return Results.Created($"/api/admin/quiz/questions/{questionId}", new { id = questionId });
        });

        quiz.MapPut("/questions/{id:guid}", async (Guid id, QuizQuestionRequest request, QuizContentService content) =>
        {
            await content.UpdateQuestionAsync(id, request);
            return Results.NoContent();
        });

        quiz.MapDelete("/questions/{id:guid}", async (Guid id, QuizContentService content) =>
        {
            await content.DeleteQuestionAsync(id);
            return Results.NoContent();
        });

        quiz.MapPost("/questions/{id:guid}/media", async (Guid id, IFormFile? file, ClaimsPrincipal user, QuizContentService content) =>
        {
            if (file is null || file.Length == 0 || file.Length > AvatarEndpoints.MaxUploadBytes)
            {
                return Results.BadRequest(new { error = "bad_file" });
            }

            await using var stream = file.OpenReadStream();
            await content.SetMediaAsync(id, stream, file.ContentType, user.UserId()!.Value);
            return Results.NoContent();
        }).DisableAntiforgery();

        quiz.MapDelete("/questions/{id:guid}/media", async (Guid id, QuizContentService content) =>
        {
            await content.RemoveMediaAsync(id);
            return Results.NoContent();
        });

        quiz.MapGet("/export", (QuizContentService content) => content.ExportAsync());

        quiz.MapPost("/import", async (QuizImport import, QuizContentService content) =>
        {
            await content.ImportAsync(import);
            return Results.NoContent();
        });

        return app;
    }
}
