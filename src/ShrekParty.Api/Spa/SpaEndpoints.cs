using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Net.Http.Headers;

namespace ShrekParty.Api.Spa;

/// <summary>
/// Раздача собранного фронта (wwwroot) и работа за обратным прокси.
/// </summary>
public static class SpaEndpoints
{
    /// <summary>
    /// Заголовки от Caddy: схема (https) и адрес клиента — иначе cookie и ссылки считают запрос http.
    /// </summary>
    public static IServiceCollection AddShrekProxy(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Caddy в той же docker-сети, его адрес заранее неизвестен.
            o.KnownIPNetworks.Clear();
            o.KnownProxies.Clear();
        });

    /// <summary>
    /// Статика и fallback на index.html для клиентских маршрутов (/tv, /admin…).
    /// Неизвестные /api и /media — честный 404, а не страница.
    /// </summary>
    public static WebApplication MapSpa(this WebApplication app)
    {
        app.MapFallback("/api/{**rest}", () => Results.NotFound());
        app.MapFallback("/media/{**rest}", () => Results.NotFound());
        app.MapFallbackToFile("index.html", new StaticFileOptions
        {
            // index.html всегда свежий — после передеплоя телефоны не застрянут на старой сборке.
            OnPrepareResponse = ctx => ctx.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache",
        });
        return app;
    }
}
