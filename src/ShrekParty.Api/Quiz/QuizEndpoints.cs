using System.Security.Claims;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Events;

namespace ShrekParty.Api.Quiz;

/// <summary>Выбор клетки.</summary>
public record QuizChooseRequest(Guid QuestionId);

/// <summary>Нажатие: момент по часам сервера, как его посчитал телефон.</summary>
public record QuizPressRequest(DateTimeOffset? ClientTime);

/// <summary>Вердикт по ответу.</summary>
public record QuizVerdictRequest(bool Correct);

/// <summary>Ставка в финале.</summary>
public record QuizBetRequest(int Amount);

/// <summary>Ответ в финале.</summary>
public record QuizFinalAnswerRequest(string? Text);

/// <summary>Отметка по финалисту.</summary>
public record QuizFinalVerdictRequest(Guid UserId, bool Correct);

/// <summary>Команда медиа.</summary>
public record QuizMediaRequest(QuizMediaAction Action);

/// <summary>
/// Маршруты игры: игрок — выбор и кнопка, админ — пульт.
/// </summary>
public static class QuizEndpoints
{
    /// <summary>
    /// <c>/api/quiz/{id}/*</c> (игрок) и <c>/api/admin/quiz/{id}/*</c> (пульт).
    /// </summary>
    public static IEndpointRouteBuilder MapQuiz(this IEndpointRouteBuilder app)
    {
        var routes = app.WithRuleErrors();
        var player = routes.MapGroup("/api/quiz/{id:guid}").RequireAuthorization(AuthEndpoints.PlayerPolicy);
        var admin = routes.MapGroup("/api/admin/quiz/{id:guid}").RequireAuthorization(AuthEndpoints.AdminPolicy);

        player.MapPost("/choose", async (Guid id, QuizChooseRequest body, ClaimsPrincipal user, QuizService quiz) =>
        {
            await quiz.ChooseAsync(id, body.QuestionId, user.UserId()!.Value);
            return Results.NoContent();
        });

        player.MapPost("/press", async (Guid id, QuizPressRequest body, ClaimsPrincipal user, QuizService quiz) =>
        {
            await quiz.PressAsync(id, user.UserId()!.Value, body.ClientTime);
            return Results.NoContent();
        });

        player.MapPost("/bet", async (Guid id, QuizBetRequest body, ClaimsPrincipal user, QuizService quiz) =>
        {
            await quiz.BetAsync(id, user.UserId()!.Value, body.Amount);
            return Results.NoContent();
        });

        player.MapPost("/final-answer", async (Guid id, QuizFinalAnswerRequest body, ClaimsPrincipal user, QuizService quiz) =>
        {
            await quiz.FinalAnswerAsync(id, user.UserId()!.Value, body.Text);
            return Results.NoContent();
        });

        admin.MapPost("/final", async (Guid id, QuizService quiz) =>
        {
            await quiz.StartFinalAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/final-verdict", async (Guid id, QuizFinalVerdictRequest body, QuizService quiz) =>
        {
            await quiz.FinalVerdictAsync(id, body.UserId, body.Correct);
            return Results.NoContent();
        });

        admin.MapPost("/reveal", async (Guid id, QuizService quiz) =>
        {
            await quiz.RevealAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/choose", async (Guid id, QuizChooseRequest body, QuizService quiz) =>
        {
            await quiz.ChooseAsync(id, body.QuestionId, null);
            return Results.NoContent();
        });

        admin.MapPost("/verdict", async (Guid id, QuizVerdictRequest body, QuizService quiz) =>
        {
            await quiz.VerdictAsync(id, body.Correct);
            return Results.NoContent();
        });

        admin.MapPost("/close", async (Guid id, QuizService quiz) =>
        {
            await quiz.CloseAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/next-round", async (Guid id, QuizService quiz) =>
        {
            await quiz.NextRoundAsync(id);
            return Results.NoContent();
        });

        admin.MapPost("/media", async (Guid id, QuizMediaRequest body, QuizService quiz) =>
        {
            await quiz.MediaAsync(id, body.Action);
            return Results.NoContent();
        });

        return app;
    }
}
