using ShrekParty.Api.Auth;

namespace ShrekParty.Api.Tv;

/// <summary>
/// Данные для экрана ТВ.
/// </summary>
public static class TvEndpoints
{
    /// <summary>
    /// Маршруты <c>/api/tv</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapTv(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tv/facts", (TvFactsService facts) => facts.GetFactsAsync())
            .RequireAuthorization(AuthEndpoints.ViewerPolicy);

        return app;
    }
}
