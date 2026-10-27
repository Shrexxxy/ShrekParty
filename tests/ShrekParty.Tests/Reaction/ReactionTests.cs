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
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Reaction;

public class ReactionTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_red_then_green_after_random_delay()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (_, admin) = await StartAsync();
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Red");

        await AdvanceAsync(TimeSpan.FromSeconds(1.9));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Red");

        await AdvanceAsync(TimeSpan.FromSeconds(4.2));
        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Green");
        details.GetProperty("round").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Green_moment_hidden_while_red()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (_, admin) = await StartAsync();

        var details = await DetailsAsync(await PlayerAsync("Ann"));

        details.GetProperty("deadline").ValueKind.Should().Be(JsonValueKind.Null);
        (await DetailsAsync(admin)).GetProperty("deadline").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Attempt_in_green_saved()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartGreenAsync();
        var ann = await PlayerAsync("Ann");

        await AttemptOkAsync(ann, id, 312);

        var details = await DetailsAsync(ann);
        details.GetProperty("myAttempt").GetInt32().Should().Be(312);
        Row(details, "Ann").GetProperty("best").GetInt32().Should().Be(312);
    }

    [Fact]
    public async Task Attempt_twice_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartGreenAsync();
        var ann = await PlayerAsync("Ann");
        await AttemptOkAsync(ann, id, 312);

        (await AttemptAsync(ann, id, 200)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Attempt_in_red_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync();

        (await AttemptAsync(await PlayerAsync("Ann"), id, 312)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bad_time_400()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartGreenAsync();

        (await AttemptAsync(await PlayerAsync("Ann"), id, 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task False_start_burns_round()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync();
        var ann = await PlayerAsync("Ann");

        (await ann.PostAsync($"/api/reaction/{id}/false-start", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AdvanceAsync(TimeSpan.FromSeconds(6.1));

        (await AttemptAsync(ann, id, 300)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var row = Row(await DetailsAsync(ann), "Ann");
        row.GetProperty("attempts")[0].GetProperty("falseStart").GetBoolean().Should().BeTrue();
        row.GetProperty("best").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Round_cycle_three_rounds_then_finish()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync();

        for (var round = 1; round <= 3; round++)
        {
            await AdvanceAsync(TimeSpan.FromSeconds(6.1));
            var green = await DetailsAsync(admin);
            green.GetProperty("phase").GetString().Should().Be("Green");
            green.GetProperty("round").GetInt32().Should().Be(round);

            await AdvanceAsync(TimeSpan.FromSeconds(5));
            (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Result");

            await AdvanceAsync(TimeSpan.FromSeconds(4));
        }

        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
    }

    [Fact]
    public async Task Rank_by_best_time_dense()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        var (id, admin) = await StartGreenAsync();
        var (ann, bob, cid) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"), await PlayerAsync("Cid"));
        await AttemptOkAsync(ann, id, 400);
        await AttemptOkAsync(bob, id, 250);
        await AttemptOkAsync(cid, id, 250);
        await AdvanceAsync(TimeSpan.FromSeconds(5));
        await AdvanceAsync(TimeSpan.FromSeconds(4));
        await AdvanceAsync(TimeSpan.FromSeconds(6.1));
        await AttemptOkAsync(ann, id, 200);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        // Ann — 200 (лучшая из двух), Bob и Cid делят второе: Linear, 2 места, максимум 30.
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 30, ["Bob"] = 15, ["Cid"] = 15 });
    }

    [Fact]
    public async Task No_valid_attempt_not_ranked()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartGreenAsync();
        await AttemptOkAsync(await PlayerAsync("Ann"), id, 300);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 30, ["Bob"] = 0 });
    }

    public void Dispose() => _factory.Dispose();

    private async Task<(Guid Id, HttpClient Admin)> StartAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Reaction", title = "Реакция" }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> StartGreenAsync()
    {
        var started = await StartAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(6.1));
        return started;
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> AttemptAsync(HttpClient player, Guid id, int ms) =>
        player.PostAsJsonAsync($"/api/reaction/{id}/attempt", new { ms });

    private static async Task AttemptOkAsync(HttpClient player, Guid id, int ms) =>
        (await AttemptAsync(player, id, ms)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static JsonElement Row(JsonElement details, string login) =>
        details.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("login").GetString() == login);

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
}
