using Microsoft.EntityFrameworkCore;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Api.Media;

namespace ShrekParty.Api.Quiz;

/// <summary>Сколько медиа Своей игры ТВ уже скачало к себе.</summary>
public record TvPreloadDto(int Loaded, int Total, DateTimeOffset At);

/// <summary>Медиа для заранее скачанного кэша ТВ.</summary>
public record QuizMediaDto(string Url, QuizMediaType Type);

/// <summary>Прогресс скачивания от ТВ.</summary>
public record TvPreloadRequest(int Loaded, int Total);

/// <summary>
/// Последний прогресс предзагрузки ТВ — в памяти: после перезапуска сервера ТВ пришлёт снова.
/// </summary>
public class TvPreloadStatus
{
    /// <summary>Текущий прогресс; <c>null</c> — ТВ ещё ничего не сообщал.</summary>
    public TvPreloadDto? Current { get; set; }
}

/// <summary>
/// Маршруты предзагрузки медиа Своей игры на ТВ.
/// </summary>
public static class QuizPreloadEndpoints
{
    /// <summary>
    /// <c>GET /api/tv/quiz-media</c> и <c>POST /api/tv/preload</c> — ТВ (и админ).
    /// </summary>
    public static IEndpointRouteBuilder MapQuizPreload(this IEndpointRouteBuilder app)
    {
        var tv = app.WithRuleErrors().MapGroup("/api/tv").RequireAuthorization(AuthEndpoints.ViewerPolicy);

        tv.MapGet("/quiz-media", async (AppDbContext db) =>
            (await db.QuizQuestions.AsNoTracking().Where(q => q.MediaId != null && q.MediaType != null).ToListAsync())
            .Select(q => new QuizMediaDto(MediaStorage.UrlFor(q.MediaId!.Value), q.MediaType!.Value))
            .ToList());

        tv.MapPost("/preload", async (TvPreloadRequest body, TvPreloadStatus status, ILiveNotifier live, TimeProvider time) =>
        {
            if (body.Total < 0 || body.Loaded < 0 || body.Loaded > body.Total)
            {
                throw new BadInputException("bad_preload");
            }

            status.Current = new TvPreloadDto(body.Loaded, body.Total, time.GetUtcNow());
            await live.BroadcastAsync();
            return Results.NoContent();
        });

        return app;
    }
}
