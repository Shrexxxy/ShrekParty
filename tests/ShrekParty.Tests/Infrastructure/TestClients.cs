using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ShrekParty.Tests.Infrastructure;

/// <summary>
/// Хелперы для HTTP-клиентов под разными ролями.
/// </summary>
public static class TestClients
{
    /// <summary>JSON-настройки, совпадающие с приложением (enum строками).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Клиент с cookie, без авторизации.</summary>
    public static HttpClient Anonymous(AppFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>Клиент, вошедший как админ.</summary>
    public static Task<HttpClient> AdminAsync(AppFactory factory) =>
        LoggedInAsync(factory, AppFactory.AdminLogin, AppFactory.AdminPassword);

    /// <summary>Клиент, вошедший как ТВ.</summary>
    public static Task<HttpClient> ViewerAsync(AppFactory factory) =>
        LoggedInAsync(factory, AppFactory.ViewerLogin, AppFactory.ViewerPassword);

    /// <summary>Клиент, вошедший под указанной учёткой.</summary>
    public static async Task<HttpClient> LoggedInAsync(AppFactory factory, string login, string password)
    {
        var client = Anonymous(factory);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password });
        response.EnsureSuccessStatusCode();
        return client;
    }
}
