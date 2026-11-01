using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Events;

public class EventEngineTests : IDisposable
{
    private readonly AppFactory _factory = new(configureServices: s => s.AddScoped<IEventModule, FakeLaughModule>());

    [Fact]
    public async Task Create_appends_to_program_in_order()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var first = await CreateAsync(admin, "Смехлыст 1");
        var second = await CreateAsync(admin, "Смехлыст 2");

        var program = await admin.GetFromJsonAsync<List<ProgramItemDto>>("/api/admin/events", TestClients.Json);
        program!.Select(p => p.Title).Should().Equal("Смехлыст 1", "Смехлыст 2");
        second.Order.Should().BeGreaterThan(first.Order);
        first.Status.Should().Be(EventStatus.Draft);
        first.MaxDonkeys.Should().Be(100);
    }

    [Fact]
    public async Task Create_unsupported_type_returns_400()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Registration", title = "Ещё одна" }, TestClients.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_second_active_returns_409()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var first = await CreateAsync(admin, "Первый");
        var second = await CreateAsync(admin, "Второй");
        (await admin.PostAsync($"/api/admin/events/{first.Id}/start", null)).EnsureSuccessStatusCode();

        var response = await admin.PostAsync($"/api/admin/events/{second.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("already_active");
    }

    [Fact]
    public async Task Start_while_registration_open_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Рано");

        var response = await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Finish_awards_linear()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Смехлыст");
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();

        var finish = await admin.PostAsync($"/api/admin/events/{ev.Id}/finish", null);

        finish.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var tab = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={ev.Id}", TestClients.Json);
        tab!.Select(r => (r.Login, r.Donkeys)).Should().Equal(("Ann", 100), ("Bob", 50));
        var program = await admin.GetFromJsonAsync<List<ProgramItemDto>>("/api/admin/events", TestClients.Json);
        program!.Single().Status.Should().Be(EventStatus.Finished);
    }

    [Fact]
    public async Task Requested_scheme_ignored_always_linear()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Laugh", title = "Смехлыст", scheme = "Podium" }, TestClients.Json);
        var ev = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!;
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();

        (await admin.PostAsync($"/api/admin/events/{ev.Id}/finish", null)).EnsureSuccessStatusCode();

        var tab = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={ev.Id}", TestClients.Json);
        tab!.Select(r => (r.Login, r.Donkeys)).Should().Equal(("Ann", 100), ("Bob", 50));
    }

    [Fact]
    public async Task Fight_poll_no_longer_exists()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/admin/events", new { type = "FightPoll", title = "Опрос" }, TestClients.Json);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Concurrent_finish_awards_once()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Смехлыст");
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();

        // Таймер и кнопка «Завершить» в одну секунду: оба уже посчитали места.
        FakeLaughModule.RankBarrier = new Barrier(2);
        try
        {
            var outcomes = new EngineOutcome[2];
            await Task.WhenAll(
                Task.Run(() => _factory.WithServiceAsync<EventEngine>(async e => outcomes[0] = await e.FinishAsync(ev.Id))),
                Task.Run(() => _factory.WithServiceAsync<EventEngine>(async e => outcomes[1] = await e.FinishAsync(ev.Id))));

            outcomes.Should().BeEquivalentTo([EngineOutcome.Ok, EngineOutcome.NotActive]);
        }
        finally
        {
            FakeLaughModule.RankBarrier = null;
        }

        var tab = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={ev.Id}", TestClients.Json);
        tab!.Select(r => (r.Login, r.Donkeys)).Should().Equal(("Ann", 100), ("Bob", 50));
    }

    [Fact]
    public async Task Finish_not_active_returns_409()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Не начат");

        var response = await admin.PostAsync($"/api/admin/events/{ev.Id}/finish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Snapshot_contains_active_event_with_details_per_viewer()
    {
        var playerId = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Смехлыст");
        await using var player = await LiveClient.ConnectAsync(
            _factory, await AuthCookies.LoginAsync(_factory, "Ann", TestData.PlayerPassword));

        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();

        var snapshot = await player.WaitForAsync(s => s.ActiveEvent is not null);
        snapshot.ActiveEvent!.Type.Should().Be(EventType.Laugh);
        snapshot.ActiveEvent.Title.Should().Be("Смехлыст");
        var details = (JsonElement)snapshot.ActiveEvent.Details!;
        details.GetProperty("viewerId").GetGuid().Should().Be(playerId);
        details.GetProperty("isAdmin").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Snapshot_has_no_active_event_after_finish()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Смехлыст");
        await using var watcher = await LiveClient.ConnectAsync(
            _factory, await AuthCookies.LoginAsync(_factory, AppFactory.AdminLogin, AppFactory.AdminPassword));
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();
        await watcher.WaitForAsync(s => s.ActiveEvent is not null);

        (await admin.PostAsync($"/api/admin/events/{ev.Id}/finish", null)).EnsureSuccessStatusCode();

        await watcher.WaitForAsync(s => s.ActiveEvent is null);
    }

    [Fact]
    public async Task Tick_reaches_active_module()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Смехлыст");
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();
        var before = FakeLaughModule.Ticks;

        await _factory.WithServiceAsync<EventEngine>(engine => engine.TickAsync());

        FakeLaughModule.Ticks.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task Delete_only_draft()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var draft = await CreateAsync(admin, "Черновик");
        var active = await CreateAsync(admin, "Идёт");
        (await admin.PostAsync($"/api/admin/events/{active.Id}/start", null)).EnsureSuccessStatusCode();

        (await admin.DeleteAsync($"/api/admin/events/{draft.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/admin/events/{active.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reorder_moves_event()
    {
        var admin = await TestClients.AdminAsync(_factory);
        await CreateAsync(admin, "А");
        var b = await CreateAsync(admin, "Б");

        (await admin.PutAsJsonAsync($"/api/admin/events/{b.Id}/order", new { order = 0 })).EnsureSuccessStatusCode();

        var program = await admin.GetFromJsonAsync<List<ProgramItemDto>>("/api/admin/events", TestClients.Json);
        program!.Select(p => p.Title).Should().Equal("Б", "А");
    }

    [Fact]
    public async Task Reset_keeps_program_as_draft()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var ev = await CreateAsync(admin, "Смехлыст");
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/start", null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/events/{ev.Id}/finish", null)).EnsureSuccessStatusCode();

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        var program = await admin.GetFromJsonAsync<List<ProgramItemDto>>("/api/admin/events", TestClients.Json);
        program!.Single().Status.Should().Be(EventStatus.Draft);
    }

    [Fact]
    public async Task Player_cannot_manage_program()
    {
        await _factory.SeedPlayerAsync("Ann");
        var player = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);

        var response = await player.PostAsJsonAsync("/api/admin/events", new { type = "Laugh", title = "Хитрость" }, TestClients.Json);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<ProgramItemDto> CreateAsync(HttpClient admin, string title)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Laugh", title }, TestClients.Json);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!;
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
}
