using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Tapper;

public class TapperTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Countdown_then_running()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (_, admin) = await StartAsync(_factory);
        var countdown = await DetailsAsync(admin);
        countdown.GetProperty("phase").GetString().Should().Be("Countdown");
        countdown.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(3));

        await AdvanceAsync(_factory, TimeSpan.FromSeconds(3));

        var running = await DetailsAsync(admin);
        running.GetProperty("phase").GetString().Should().Be("Running");
        running.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(60));
    }

    [Fact]
    public async Task Taps_accumulate()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartRunningAsync();
        var ann = await PlayerAsync("Ann");

        await TapsOkAsync(ann, id, 5);
        await TapsOkAsync(ann, id, 7);

        Taps(await DetailsAsync(admin))["Ann"].Should().Be(12);
    }

    [Fact]
    public async Task Taps_before_start_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(_factory);

        (await TapsAsync(await PlayerAsync("Ann"), id, 5)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bad_count_400()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartRunningAsync();

        (await TapsAsync(await PlayerAsync("Ann"), id, 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Huge_batch_400()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartRunningAsync();

        (await TapsAsync(await PlayerAsync("Ann"), id, 2_000_000_000)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Last_batch_after_deadline_accepted()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartRunningAsync();
        await AdvanceAsync(_factory, TimeSpan.FromSeconds(60.5));

        await TapsOkAsync(await PlayerAsync("Ann"), id, 3);

        Taps(await DetailsAsync(admin))["Ann"].Should().Be(3);
    }

    [Fact]
    public async Task Taps_too_late_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartRunningAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(61.5));

        (await TapsAsync(await PlayerAsync("Ann"), id, 3)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Running_broadcasts_every_tick()
    {
        var notifier = new CountingNotifier();
        using var factory = new AppFactory(configureServices: s => s.AddSingleton<ILiveNotifier>(notifier));
        await factory.SeedPlayerAsync("Ann");
        await StartAsync(factory);
        await AdvanceAsync(factory, TimeSpan.FromSeconds(3));
        var before = notifier.Count;

        await AdvanceAsync(factory, TimeSpan.FromSeconds(1));
        await AdvanceAsync(factory, TimeSpan.FromSeconds(1));

        notifier.Count.Should().Be(before + 2);
    }

    [Fact]
    public async Task Taps_do_not_broadcast()
    {
        var notifier = new CountingNotifier();
        using var factory = new AppFactory(configureServices: s => s.AddSingleton<ILiveNotifier>(notifier));
        await factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(factory);
        await AdvanceAsync(factory, TimeSpan.FromSeconds(3));
        var ann = await TestClients.LoggedInAsync(factory, "Ann", TestData.PlayerPassword);
        var before = notifier.Count;

        await TapsOkAsync(ann, id, 5);

        notifier.Count.Should().Be(before);
    }

    [Fact]
    public async Task Finish_by_deadline_ranks_by_taps()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        var (id, admin) = await StartRunningAsync();
        await TapsOkAsync(await PlayerAsync("Ann"), id, 100);
        var bob = await PlayerAsync("Bob");
        for (var i = 0; i < 3; i++)
        {
            await TapsOkAsync(bob, id, 100);
        }

        await TapsOkAsync(await PlayerAsync("Cid"), id, 100);

        await AdvanceAsync(_factory, TimeSpan.FromSeconds(61));

        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 30, ["Ann"] = 15, ["Cid"] = 15 });
    }

    [Fact]
    public async Task Zero_taps_not_ranked()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartRunningAsync();
        await TapsOkAsync(await PlayerAsync("Ann"), id, 10);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 30, ["Bob"] = 0 });
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<(Guid Id, HttpClient Admin)> StartAsync(AppFactory factory)
    {
        var admin = await TestClients.AdminAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Tapper", title = "Тапалка" }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> StartRunningAsync()
    {
        var started = await StartAsync(_factory);
        await AdvanceAsync(_factory, TimeSpan.FromSeconds(3));
        return started;
    }

    private static async Task AdvanceAsync(AppFactory factory, TimeSpan by)
    {
        factory.Time.Advance(by);
        await factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> TapsAsync(HttpClient player, Guid id, int count) =>
        player.PostAsJsonAsync($"/api/tapper/{id}/taps", new { count });

    private static async Task TapsOkAsync(HttpClient player, Guid id, int count) =>
        (await TapsAsync(player, id, count)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static Dictionary<string, int> Taps(JsonElement details) =>
        details.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("login").GetString()!, r => r.GetProperty("taps").GetInt32());

    private async Task<EventStatus> StatusAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Events.SingleAsync(e => e.Id == id)).Status;
    }

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid id)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }

    /// <summary>Считает рассылки вместо отправки.</summary>
    private sealed class CountingNotifier : ILiveNotifier
    {
        public int Count;

        public Task BroadcastAsync()
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }
}
