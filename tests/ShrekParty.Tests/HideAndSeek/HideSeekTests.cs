using System.Net;
using System.Net.Http.Headers;
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

namespace ShrekParty.Tests.HideAndSeek;

public class HideSeekTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_hiding_2_minutes()
    {
        await _factory.SeedPlayerAsync("Ann");

        var (_, admin) = await StartAsync();

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Hiding");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddMinutes(2));
    }

    [Fact]
    public async Task Start_without_players_409()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Tick_hiding_to_shooting_10_minutes()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (_, admin) = await StartAsync();

        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        await TickAsync();

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Shooting");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddMinutes(10));
    }

    [Fact]
    public async Task Shot_marks_players_and_counts_hits()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartShootingAsync();

        await ShotOkAsync(admin, id, ann, bob);
        await ShotOkAsync(admin, id, ann);

        var details = await DetailsAsync(admin);
        Hits(details).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 2, ["Bob"] = 1 });
        details.GetProperty("shots").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Shot_without_players_saved()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();

        await ShotOkAsync(admin, id);

        var shot = (await DetailsAsync(admin)).GetProperty("shots").EnumerateArray().Single();
        shot.GetProperty("userIds").GetArrayLength().Should().Be(0);
        shot.GetProperty("url").GetString().Should().StartWith("/media/");
    }

    [Fact]
    public async Task Shot_during_hiding_409()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync();

        (await ShotAsync(admin, id, ann)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Edit_marks()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartShootingAsync();
        var shot = await ShotOkAsync(admin, id, ann);

        (await admin.PutAsJsonAsync($"/api/admin/hide/shots/{shot}/players", new { userIds = new[] { bob } }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        Hits(await DetailsAsync(admin)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 0, ["Bob"] = 1 });
    }

    [Fact]
    public async Task Mark_non_player_400()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();

        (await ShotAsync(admin, id, Guid.NewGuid())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_shot_before_voting()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        var shot = await ShotOkAsync(admin, id, ann);

        (await admin.DeleteAsync($"/api/admin/hide/shots/{shot}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DetailsAsync(admin)).GetProperty("shots").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Delete_shot_in_voting_409()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        var shot = await ShotOkAsync(admin, id, ann);
        await StopAsync(admin, id);

        (await admin.DeleteAsync($"/api/admin/hide/shots/{shot}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Stop_early_starts_voting()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        await ShotOkAsync(admin, id, ann);
        _factory.Time.Advance(TimeSpan.FromMinutes(3));

        await StopAsync(admin, id);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Voting");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddMinutes(10));
    }

    [Fact]
    public async Task Shooting_deadline_starts_voting()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        await ShotOkAsync(admin, id, ann);

        _factory.Time.Advance(TimeSpan.FromMinutes(10));
        await TickAsync();

        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Voting");
    }

    [Fact]
    public async Task Shooting_deadline_without_shots_finishes()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartShootingAsync();

        _factory.Time.Advance(TimeSpan.FromMinutes(10));
        await TickAsync();

        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
    }

    [Fact]
    public async Task Stop_without_shots_finishes()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();

        await StopAsync(admin, id);

        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
    }

    [Fact]
    public async Task Late_registrant_not_ranked()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        await ShotOkAsync(admin, id, ann);
        await _factory.SeedPlayerAsync("Cid");
        await StopAsync(admin, id);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 100, ["Cid"] = 0 });
    }

    [Fact]
    public async Task Rapid_votes_keep_one_vote()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartShootingAsync();
        var first = await ShotOkAsync(admin, id, ann);
        var second = await ShotOkAsync(admin, id);
        await StopAsync(admin, id);
        var bob = await PlayerAsync("Bob");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => VoteAsync(bob, id, i % 2 == 0 ? first : second)));

        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
        Votes(await DetailsAsync(bob)).Values.Sum().Should().Be(1);
    }

    [Fact]
    public async Task Players_do_not_see_shots_while_shooting()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        await ShotOkAsync(admin, id, ann);

        var details = await DetailsAsync(await PlayerAsync("Ann"));

        details.GetProperty("shots").GetArrayLength().Should().Be(0);
        details.GetProperty("players").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Vote_and_change_vote()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartShootingAsync();
        var first = await ShotOkAsync(admin, id, ann);
        var second = await ShotOkAsync(admin, id);
        await StopAsync(admin, id);
        var bob = await PlayerAsync("Bob");

        await VoteOkAsync(bob, id, first);
        await VoteOkAsync(bob, id, second);

        var details = await DetailsAsync(bob);
        details.GetProperty("myVote").GetGuid().Should().Be(second);
        Votes(details).Should().BeEquivalentTo(new Dictionary<Guid, int> { [first] = 0, [second] = 1 });
    }

    [Fact]
    public async Task Vote_outside_voting_409()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        var shot = await ShotOkAsync(admin, id, ann);

        (await VoteAsync(await PlayerAsync("Ann"), id, shot)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_cannot_vote()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        var shot = await ShotOkAsync(admin, id, ann);
        await StopAsync(admin, id);

        (await VoteAsync(admin, id, shot)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Voting_deadline_finishes_and_ranks_fewest_hits_first()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        var (id, admin) = await StartShootingAsync();
        await ShotOkAsync(admin, id, ann, bob);
        await ShotOkAsync(admin, id, ann);
        await StopAsync(admin, id);

        _factory.Time.Advance(TimeSpan.FromMinutes(10));
        await TickAsync();

        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Cid"] = 100, ["Bob"] = 67, ["Ann"] = 33 });
    }

    [Fact]
    public async Task Funniest_photo_gives_15_bonus()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        var (id, admin) = await StartShootingAsync();
        var funny = await ShotOkAsync(admin, id, ann, bob);
        var boring = await ShotOkAsync(admin, id, ann);
        await StopAsync(admin, id);
        await VoteOkAsync(await PlayerAsync("Cid"), id, funny);
        await VoteOkAsync(await PlayerAsync("Bob"), id, funny);
        await VoteOkAsync(await PlayerAsync("Ann"), id, boring);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Cid"] = 100, ["Bob"] = 67 + 15, ["Ann"] = 33 + 15 });
    }

    [Fact]
    public async Task Tie_gives_bonus_to_all_winners_once()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartShootingAsync();
        var first = await ShotOkAsync(admin, id, ann);
        var second = await ShotOkAsync(admin, id, ann, bob);
        await StopAsync(admin, id);
        await VoteOkAsync(await PlayerAsync("Ann"), id, second);
        await VoteOkAsync(await PlayerAsync("Bob"), id, first);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bonuses = await db.DonkeyAwards.Where(a => a.EventId == id && a.Reason == "Самое смешное фото").ToListAsync();
        bonuses.Select(b => (b.UserId, b.Amount)).Should().BeEquivalentTo([(ann, 15), (bob, 15)]);
    }

    [Fact]
    public async Task No_votes_no_bonus()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartShootingAsync();
        await ShotOkAsync(admin, id, ann);
        await StopAsync(admin, id);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DonkeyAwards.AnyAsync(a => a.Reason == "Самое смешное фото")).Should().BeFalse();
    }

    [Fact]
    public async Task Reset_clears_hide_data()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartShootingAsync();
        var shot = await ShotOkAsync(admin, id, ann);
        await StopAsync(admin, id);
        await VoteOkAsync(await PlayerAsync("Bob"), id, shot);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.HideShots.AnyAsync()).Should().BeFalse();
        (await db.HideShotPlayers.AnyAsync()).Should().BeFalse();
        (await db.HideVotes.AnyAsync()).Should().BeFalse();
        (await db.MediaFiles.AnyAsync()).Should().BeFalse();
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "HideAndSeek", title = "Прятки" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> StartShootingAsync()
    {
        var started = await StartAsync();
        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        await TickAsync();
        return started;
    }

    private Task TickAsync() => _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> ShotAsync(HttpClient admin, Guid id, params Guid[] userIds)
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var form = new MultipartFormDataContent
        {
            { file, "file", "shot.jpg" },
            { new StringContent(string.Join(',', userIds)), "userIds" },
        };
        return admin.PostAsync($"/api/admin/hide/{id}/shots", form);
    }

    private static async Task<Guid> ShotOkAsync(HttpClient admin, Guid id, params Guid[] userIds)
    {
        var response = await ShotAsync(admin, id, userIds);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task StopAsync(HttpClient admin, Guid id) =>
        (await admin.PostAsync($"/api/admin/hide/{id}/stop", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static Task<HttpResponseMessage> VoteAsync(HttpClient client, Guid id, Guid shotId) =>
        client.PostAsJsonAsync($"/api/hide/{id}/vote", new { shotId });

    private static async Task VoteOkAsync(HttpClient client, Guid id, Guid shotId) =>
        (await VoteAsync(client, id, shotId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static Dictionary<string, int> Hits(JsonElement details) =>
        details.GetProperty("players").EnumerateArray()
            .ToDictionary(p => p.GetProperty("login").GetString()!, p => p.GetProperty("hits").GetInt32());

    private static Dictionary<Guid, int> Votes(JsonElement details) =>
        details.GetProperty("shots").EnumerateArray()
            .ToDictionary(s => s.GetProperty("id").GetGuid(), s => s.GetProperty("votes").GetInt32());

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
