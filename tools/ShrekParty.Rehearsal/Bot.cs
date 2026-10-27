using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace ShrekParty.Rehearsal;

/// <summary>Настройки бота.</summary>
/// <param name="BaseUrl">Адрес API.</param>
/// <param name="MinDelayMs">Минимальная «человеческая» пауза перед действием.</param>
/// <param name="MaxDelayMs">Максимальная пауза перед действием.</param>
/// <param name="PollMs">Как часто спрашивать, открыта ли регистрация.</param>
public record BotOptions(Uri BaseUrl, int MinDelayMs = 1000, int MaxDelayMs = 6000, int PollMs = 2000);

/// <summary>Как ходить в сеть: <c>null</c> — обычная сеть; иначе свой обработчик (тестовый сервер, только long polling).</summary>
public record BotTransport(Func<HttpMessageHandler>? Handler = null);

/// <summary>
/// Бот-гость: регистрируется, слушает снапшоты и сам реагирует на ивенты — как живой гость на репетиции.
/// </summary>
public sealed class Bot(string login, BotOptions options, BotTransport transport, TextWriter log, Random random)
{
    /// <summary>Сильный пароль бота — с ним же бот входит при перезапуске.</summary>
    public const string StrongPassword = "Swamp-Rehearsal-Bot-2026!";

    private static readonly string[] WeakPasswords = ["12345", "qwerty", "password", "shrek", "123456789", "ogre"];

    private readonly CookieContainer _cookies = new();
    private readonly HashSet<string> _done = [];

    /// <summary>Логин бота.</summary>
    public string Login => login;

    /// <summary>Id игрока после регистрации или входа.</summary>
    public Guid? UserId { get; private set; }

    /// <summary>Подключён ли к живому каналу.</summary>
    public bool Connected { get; private set; }

    /// <summary>
    /// Живёт до отмены: регистрация → подключение → реакция на снапшоты.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var http = new HttpClient(new CookieHandler(_cookies) { InnerHandler = transport.Handler?.Invoke() ?? new SocketsHttpHandler { UseCookies = false } })
            {
                BaseAddress = options.BaseUrl,
            };
            await SignInAsync(http, ct);
            await ListenAsync(http, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Репетиция окончена.
        }
    }

    private async Task SignInAsync(HttpClient http, CancellationToken ct)
    {
        while (true)
        {
            var status = await http.GetFromJsonAsync<JsonElement>("/api/registration/status", ct);
            if (status.GetProperty("state").GetString() == "Closed")
            {
                await Task.Delay(options.PollMs, ct);
                continue;
            }

            // Гости подтягиваются не одновременно.
            await PauseAsync(ct);
            var start = await http.PostAsJsonAsync("/api/registration/start", new { login, password = WeakPasswords[random.Next(WeakPasswords.Length)] }, ct);
            if (start.StatusCode == HttpStatusCode.Conflict)
            {
                // Логин уже есть — бота перезапустили.
                var signIn = await http.PostAsJsonAsync("/api/auth/login", new { login, password = StrongPassword }, ct);
                signIn.EnsureSuccessStatusCode();
                UserId = (await signIn.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
                log.WriteLine($"[{login}] вошёл снова");
                return;
            }

            if (start.StatusCode == HttpStatusCode.Forbidden)
            {
                await Task.Delay(options.PollMs, ct);
                continue;
            }

            start.EnsureSuccessStatusCode();
            var attemptId = (await start.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("attemptId").GetGuid();
            var complete = await http.PostAsJsonAsync("/api/registration/complete", new { attemptId, newPassword = StrongPassword }, ct);
            complete.EnsureSuccessStatusCode();
            UserId = (await complete.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetGuid();
            log.WriteLine($"[{login}] зарегистрировался");
            return;
        }
    }

    private async Task ListenAsync(HttpClient http, CancellationToken ct)
    {
        var snapshots = Channel.CreateUnbounded<JsonElement>(new UnboundedChannelOptions { SingleReader = true });
        await using var hub = new HubConnectionBuilder()
            .WithUrl(new Uri(options.BaseUrl, "/hub"), o =>
            {
                o.Headers["Cookie"] = _cookies.GetCookieHeader(options.BaseUrl);
                if (transport.Handler is { } handler)
                {
                    o.Transports = HttpTransportType.LongPolling;
                    o.HttpMessageHandlerFactory = _ => handler();
                }
            })
            .WithAutomaticReconnect()
            .Build();

        hub.On<JsonElement>("Snapshot", s => snapshots.Writer.TryWrite(s));
        hub.Reconnecting += _ =>
        {
            Connected = false;
            return Task.CompletedTask;
        };
        hub.Reconnected += _ =>
        {
            Connected = true;
            return Task.CompletedTask;
        };

        await hub.StartAsync(ct);
        Connected = true;

        while (await snapshots.Reader.WaitToReadAsync(ct))
        {
            // Пока бот «думал», могли прийти новые снапшоты — важен только последний.
            var latest = default(JsonElement);
            while (snapshots.Reader.TryRead(out var s))
            {
                latest = s;
            }

            if (latest.ValueKind == JsonValueKind.Object
                && latest.TryGetProperty("activeEvent", out var active) && active.ValueKind == JsonValueKind.Object)
            {
                foreach (var action in BotBrain.Decide(active, UserId!.Value, random, _done))
                {
                    await PauseAsync(ct);
                    await DoAsync(http, action, ct);
                }
            }
        }
    }

    private async Task DoAsync(HttpClient http, BotAction action, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(action.Method, action.Path);
        if (action.Photo)
        {
            var file = new ByteArrayContent(TinyPng.Create(random));
            file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            request.Content = new MultipartFormDataContent { { file, "file", "bot.png" } };
        }
        else if (action.Body is not null)
        {
            request.Content = JsonContent.Create(action.Body);
        }

        using var response = await http.SendAsync(request, ct);
        var error = response.IsSuccessStatusCode ? "" : $" → {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}";
        log.WriteLine($"[{login}] {action.Description}{error}");
    }

    private Task PauseAsync(CancellationToken ct)
    {
        var ms = random.Next(options.MinDelayMs, options.MaxDelayMs + 1);
        return ms == 0 ? Task.CompletedTask : Task.Delay(ms, ct);
    }
}
