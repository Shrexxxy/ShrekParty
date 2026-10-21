using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Live;

namespace ShrekParty.Tests.Infrastructure;

/// <summary>
/// SignalR-клиент для тестов: собирает все пришедшие снапшоты.
/// </summary>
public sealed class LiveClient : IAsyncDisposable
{
    private readonly HubConnection _connection;
    private readonly Channel<AppSnapshot> _snapshots = Channel.CreateUnbounded<AppSnapshot>();

    private LiveClient(HubConnection connection)
    {
        _connection = connection;
        _connection.On<AppSnapshot>("Snapshot", s =>
        {
            Last = s;
            _snapshots.Writer.TryWrite(s);
        });
    }

    /// <summary>Последний полученный снапшот.</summary>
    public AppSnapshot? Last { get; private set; }

    /// <summary>Подключается к хабу; <paramref name="cookie"/> — значение заголовка Cookie или <c>null</c> для гостя.</summary>
    public static async Task<LiveClient> ConnectAsync(AppFactory factory, string? cookie = null)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "/hub"), o =>
            {
                o.Transports = HttpTransportType.LongPolling;
                o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                if (cookie is not null)
                {
                    o.Headers["Cookie"] = cookie;
                }
            })
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

        var client = new LiveClient(connection);
        await connection.StartAsync();
        return client;
    }

    /// <summary>Ждёт снапшот, удовлетворяющий условию (среди уже пришедших и новых).</summary>
    public async Task<AppSnapshot> WaitForAsync(Func<AppSnapshot, bool> predicate, int timeoutMs = 5000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            await foreach (var snapshot in _snapshots.Reader.ReadAllAsync(cts.Token))
            {
                if (predicate(snapshot))
                {
                    return snapshot;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }

        throw new TimeoutException($"Нужный снапшот не пришёл за {timeoutMs} мс. Последний: {Last}");
    }

    /// <summary>Отключение.</summary>
    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

/// <summary>
/// Получение cookie авторизации для SignalR-клиента.
/// </summary>
public static class AuthCookies
{
    /// <summary>Cookie после входа под учёткой.</summary>
    public static async Task<string> LoginAsync(AppFactory factory, string login, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password });
        response.EnsureSuccessStatusCode();
        return response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }
}
