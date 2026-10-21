using System.Net.Http.Json;

namespace ShrekParty.Tests.Infrastructure;

/// <summary>
/// Шаги регистрации-ловушки через API.
/// </summary>
public static class RegistrationSteps
{
    /// <summary>Слабый первый пароль.</summary>
    public const string Weak = "12345";

    /// <summary>Сильный пароль.</summary>
    public const string Strong = "Shrek-Fiona-Donkey-42!";

    /// <summary>Ответ на старт регистрации.</summary>
    public record StartResponse(Guid AttemptId);

    /// <summary>Админ открывает регистрацию.</summary>
    public static async Task OpenRegistrationAsync(this AppFactory factory)
    {
        var admin = await TestClients.AdminAsync(factory);
        (await admin.PostAsync("/api/admin/registration/open", null)).EnsureSuccessStatusCode();
    }

    /// <summary>Шаг 1: логин и первый пароль.</summary>
    public static Task<HttpResponseMessage> StartAsync(HttpClient client, string login, string password) =>
        client.PostAsJsonAsync("/api/registration/start", new { login, password });

    /// <summary>Шаг 2: сложный пароль.</summary>
    public static Task<HttpResponseMessage> CompleteAsync(HttpClient client, Guid attemptId, string newPassword) =>
        client.PostAsJsonAsync("/api/registration/complete", new { attemptId, newPassword });

    /// <summary>Полный успешный проход; возвращает клиента, вошедшего как новый игрок.</summary>
    public static async Task<HttpClient> RegisterAsync(this AppFactory factory, string login, string first = Weak, string second = Strong)
    {
        var client = TestClients.Anonymous(factory);
        var start = await StartAsync(client, login, first);
        start.EnsureSuccessStatusCode();
        var attempt = await start.Content.ReadFromJsonAsync<StartResponse>(TestClients.Json);
        (await CompleteAsync(client, attempt!.AttemptId, second)).EnsureSuccessStatusCode();
        return client;
    }
}
