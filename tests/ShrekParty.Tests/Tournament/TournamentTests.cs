using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Tournament;

public class TournamentTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_builds_bracket_for_all_players()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan", "Eve");

        var (_, admin) = await StartTournamentAsync();

        var matches = (await DetailsAsync(admin)).GetProperty("matches").EnumerateArray().ToList();
        matches.Count(m => m.GetProperty("round").GetString() == "Quarter").Should().Be(4);
        matches.Count(m => m.GetProperty("isBye").GetBoolean()).Should().Be(3);
        matches.Where(m => m.GetProperty("isBye").GetBoolean())
            .Should().OnlyContain(m => m.GetProperty("status").GetString() == "Done");
    }

    [Fact]
    public async Task Start_with_one_player_returns_409()
    {
        await SeedPlayersAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        var response = await admin.PostAsync($"/api/admin/events/{id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("not_enough_players");
    }

    [Fact]
    public async Task Bye_winner_waits_in_next_match()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid");

        var (_, admin) = await StartTournamentAsync();

        var semis = Matches(await DetailsAsync(admin), "Semi");
        var bye = semis.Single(m => m.GetProperty("isBye").GetBoolean());
        var final = Matches(await DetailsAsync(admin), "Final").Single();
        new[] { PlayerId(final, "playerA"), PlayerId(final, "playerB") }.Should().Contain(PlayerId(bye, "playerA"));
    }

    [Fact]
    public async Task Winner_advances_to_next_match()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var semi = Matches(await DetailsAsync(admin), "Semi")[0];
        var winner = PlayerId(semi, "playerA")!.Value;

        await PlayAsync(admin, semi, winner, "3:1");

        var final = Matches(await DetailsAsync(admin), "Final").Single();
        PlayerId(final, "playerA").Should().Be(winner);
        Matches(await DetailsAsync(admin), "Semi")[0].GetProperty("score").GetString().Should().Be("3:1");
    }

    [Fact]
    public async Task Semifinal_loser_goes_to_third_place()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var semi = Matches(await DetailsAsync(admin), "Semi")[1];
        var loser = PlayerId(semi, "playerB")!.Value;

        await PlayAsync(admin, semi, PlayerId(semi, "playerA")!.Value);

        var third = Matches(await DetailsAsync(admin), "ThirdPlace").Single();
        PlayerId(third, "playerB").Should().Be(loser);
    }

    [Fact]
    public async Task Winner_must_be_match_player_400()
    {
        var ids = await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var semi = Matches(await DetailsAsync(admin), "Semi")[0];
        var stranger = ids.Except([PlayerId(semi, "playerA")!.Value, PlayerId(semi, "playerB")!.Value]).First();

        var response = await admin.PostAsJsonAsync($"/api/admin/matches/{MatchId(semi)}/winner", new { winnerId = stranger });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_match_without_both_players_409()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var final = Matches(await DetailsAsync(admin), "Final").Single();

        var response = await admin.PostAsync($"/api/admin/matches/{MatchId(final)}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("not_ready");
    }

    [Fact]
    public async Task Start_match_makes_it_live()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (_, admin) = await StartTournamentAsync();
        var final = Matches(await DetailsAsync(admin), "Final").Single();

        (await admin.PostAsync($"/api/admin/matches/{MatchId(final)}/start", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Matches(await DetailsAsync(admin), "Final").Single().GetProperty("status").GetString().Should().Be("Live");
    }

    [Fact]
    public async Task Undo_reverts_advance()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var semi = Matches(await DetailsAsync(admin), "Semi")[0];
        await PlayAsync(admin, semi, PlayerId(semi, "playerA")!.Value);

        (await admin.PostAsync($"/api/admin/matches/{MatchId(semi)}/undo", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(admin);
        var semiAfter = Matches(details, "Semi")[0];
        semiAfter.GetProperty("status").GetString().Should().Be("Live");
        semiAfter.GetProperty("winnerId").ValueKind.Should().Be(JsonValueKind.Null);
        PlayerId(Matches(details, "Final").Single(), "playerA").Should().BeNull();
        PlayerId(Matches(details, "ThirdPlace").Single(), "playerA").Should().BeNull();
    }

    [Fact]
    public async Task Undo_blocked_when_next_started()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var semis = Matches(await DetailsAsync(admin), "Semi");
        await PlayAsync(admin, semis[0], PlayerId(semis[0], "playerA")!.Value);
        await PlayAsync(admin, semis[1], PlayerId(semis[1], "playerA")!.Value);
        var final = Matches(await DetailsAsync(admin), "Final").Single();
        (await admin.PostAsync($"/api/admin/matches/{MatchId(final)}/start", null)).EnsureSuccessStatusCode();

        var response = await admin.PostAsync($"/api/admin/matches/{MatchId(semis[0])}/undo", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("next_started");
    }

    [Fact]
    public async Task Finish_before_complete_409()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();

        var response = await admin.PostAsync($"/api/admin/events/{id}/finish", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("not_complete");
    }

    [Fact]
    public async Task Finish_while_winner_decides_counts_as_decline()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();
        var (winner, _) = await PlayFinalAsync(admin, Matches(await DetailsAsync(admin), "Final").Single());

        var response = await admin.PostAsync($"/api/admin/events/{id}/finish", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 100, ["Bob"] = 50 });
        using (var scope = _factory.Services.CreateScope())
        {
            var ev = await scope.ServiceProvider.GetRequiredService<ShrekParty.Api.Data.AppDbContext>().Events.FindAsync(id);
            var state = JsonSerializer.Deserialize<ShrekParty.Api.Tournament.TournamentState>(ev!.StateJson!, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            state!.Decision.Should().Be(ShrekParty.Api.Tournament.ChallengeState.Declined);
        }

        (await winner.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = true })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Challenge_offered_only_to_final_winner()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();
        var final = Matches(await DetailsAsync(admin), "Final").Single();
        var (winner, loser) = await PlayFinalAsync(admin, final);

        var loserResponse = await loser.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = true });
        var details = await DetailsAsync(winner);

        loserResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        details.GetProperty("challenge").GetProperty("state").GetString().Should().Be("Offered");
        details.GetProperty("challenge").GetProperty("offeredToMe").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Challenge_decline_keeps_100()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();
        var (winner, _) = await PlayFinalAsync(admin, Matches(await DetailsAsync(admin), "Final").Single());

        (await winner.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = false })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 100, ["Bob"] = 50 });
    }

    [Fact]
    public async Task Challenge_accept_win_doubles_first_place()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();
        var (winner, _) = await PlayFinalAsync(admin, Matches(await DetailsAsync(admin), "Final").Single());
        (await winner.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = true })).EnsureSuccessStatusCode();

        var challenge = Matches(await DetailsAsync(admin), "Challenge").Single();
        challenge.GetProperty("playerB").GetProperty("isAdmin").GetBoolean().Should().BeTrue();
        await PlayAsync(admin, challenge, PlayerId(challenge, "playerA")!.Value);
        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 200, ["Bob"] = 50 });
    }

    [Fact]
    public async Task Challenge_accept_lose_halves_first_place()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();
        var (winner, _) = await PlayFinalAsync(admin, Matches(await DetailsAsync(admin), "Final").Single());
        (await winner.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = true })).EnsureSuccessStatusCode();

        var challenge = Matches(await DetailsAsync(admin), "Challenge").Single();
        await PlayAsync(admin, challenge, PlayerId(challenge, "playerB")!.Value);
        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 50, ["Bob"] = 50 });
    }

    [Fact]
    public async Task Challenge_decided_once()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartTournamentAsync();
        var (winner, _) = await PlayFinalAsync(admin, Matches(await DetailsAsync(admin), "Final").Single());
        (await winner.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = false })).EnsureSuccessStatusCode();

        var again = await winner.PostAsJsonAsync($"/api/tournaments/{id}/challenge", new { accept = true });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Next_match_for_player_is_shown()
    {
        await SeedPlayersAsync("Ann", "Bob");
        await StartTournamentAsync();
        var ann = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);

        var details = await DetailsAsync(ann);

        details.GetProperty("myNextMatchId").GetGuid().Should().Be(MatchId(Matches(details, "Final").Single()));
    }

    [Fact]
    public async Task Tote_tab_is_created_with_tournament()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (_, admin) = await StartTournamentAsync();

        var events = await admin.GetFromJsonAsync<List<EventDto>>("/api/events", TestClients.Json);

        events!.Select(e => e.Title).Should().Contain("Тотализатор: FIFA");
    }

    [Fact]
    public async Task Match_photo_is_linked()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (_, admin) = await StartTournamentAsync();
        var final = Matches(await DetailsAsync(admin), "Final").Single();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        var response = await admin.PostAsync($"/api/admin/matches/{MatchId(final)}/photos", new MultipartFormDataContent { { file, "file", "match.jpg" } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.MediaFiles.SingleAsync()).MatchId.Should().Be(MatchId(final));
        Matches(await DetailsAsync(admin), "Final").Single().GetProperty("photos").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Any_player_can_photo_a_live_match()
    {
        // Снимать матч может любой гость, но только пока матч идёт.
        await SeedPlayersAsync("Ann", "Bob", "Cid");
        var (_, admin) = await StartTournamentAsync();
        var cid = await TestClients.LoggedInAsync(_factory, "Cid", TestData.PlayerPassword);
        var match = (await DetailsAsync(admin)).GetProperty("matches").EnumerateArray()
            .First(m => m.GetProperty("status").GetString() == "Pending" && m.GetProperty("playerA").ValueKind != JsonValueKind.Null
                && m.GetProperty("playerB").ValueKind != JsonValueKind.Null && !m.GetProperty("isBye").GetBoolean());

        var early = await cid.PostAsync($"/api/matches/{MatchId(match)}/photos", Photo());
        (await admin.PostAsync($"/api/admin/matches/{MatchId(match)}/start", null)).EnsureSuccessStatusCode();
        var live = await cid.PostAsync($"/api/matches/{MatchId(match)}/photos", Photo());

        early.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await early.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("not_live");
        live.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetailsAsync(admin)).GetProperty("matches").EnumerateArray().Single(m => MatchId(m) == MatchId(match))
            .GetProperty("photos").GetInt32().Should().Be(1);
    }

    private static MultipartFormDataContent Photo()
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "file", "match.jpg" } };
    }

    [Fact]
    public async Task Matches_say_where_the_winner_goes_for_bracket_lines()
    {
        // Сетка рисует линии от матча к матчу, куда проходит победитель.
        await SeedPlayersAsync("Ann", "Bob", "Cid", "Dan");
        var (_, admin) = await StartTournamentAsync();
        var matches = (await DetailsAsync(admin)).GetProperty("matches").EnumerateArray().ToList();
        var final = matches.Single(m => m.GetProperty("round").GetString() == "Final");

        foreach (var semi in matches.Where(m => m.GetProperty("round").GetString() == "Semi"))
        {
            semi.GetProperty("nextIndex").GetInt32().Should().Be(final.GetProperty("index").GetInt32());
        }

        final.GetProperty("nextIndex").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Reset_clears_matches_and_match_photos()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (_, admin) = await StartTournamentAsync();
        var final = Matches(await DetailsAsync(admin), "Final").Single();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await admin.PostAsync($"/api/admin/matches/{MatchId(final)}/photos", new MultipartFormDataContent { { file, "file", "m.jpg" } })).EnsureSuccessStatusCode();

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.TournamentMatches.AnyAsync()).Should().BeFalse();
        (await db.MediaFiles.AnyAsync()).Should().BeFalse();
    }

    public void Dispose() => _factory.Dispose();

    private async Task<List<Guid>> SeedPlayersAsync(params string[] logins)
    {
        var ids = new List<Guid>();
        foreach (var login in logins)
        {
            ids.Add(await _factory.SeedPlayerAsync(login));
        }

        return ids;
    }

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Tournament", title = "FIFA" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartTournamentAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private static async Task PlayAsync(HttpClient admin, JsonElement match, Guid winner, string? score = null)
    {
        var response = await admin.PostAsJsonAsync($"/api/admin/matches/{MatchId(match)}/winner", new { winnerId = winner, score });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Финал Ann против Bob — побеждает Ann (сетка перемешана, сторону не угадываем).</summary>
    private async Task<(HttpClient Winner, HttpClient Loser)> PlayFinalAsync(HttpClient admin, JsonElement final)
    {
        await PlayAsync(admin, final, await LoginIdAsync("Ann"));
        var ann = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);
        var bob = await TestClients.LoggedInAsync(_factory, "Bob", TestData.PlayerPassword);
        return (ann, bob);
    }

    private async Task<Guid> LoginIdAsync(string login)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.SingleAsync(u => u.Login == login)).Id;
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static List<JsonElement> Matches(JsonElement details, string round) =>
        details.GetProperty("matches").EnumerateArray()
            .Where(m => m.GetProperty("round").GetString() == round)
            .OrderBy(m => m.GetProperty("roundIndex").GetInt32())
            .ToList();

    private static Guid MatchId(JsonElement match) => match.GetProperty("id").GetGuid();

    private static Guid? PlayerId(JsonElement match, string side) =>
        match.GetProperty(side).ValueKind == JsonValueKind.Null ? null : match.GetProperty(side).GetProperty("id").GetGuid();

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid eventId)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={eventId}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
}
