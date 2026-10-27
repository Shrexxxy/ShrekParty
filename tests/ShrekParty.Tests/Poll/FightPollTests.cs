using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Poll;

public class FightPollTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Default_options_are_mk_and_ufc()
    {
        var (_, admin) = await StartPollAsync();

        var details = await DetailsAsync(admin);

        details.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("name").GetString())
            .Should().Equal("Mortal Kombat", "UFC");
    }

    [Fact]
    public async Task Vote_counts_and_changes()
    {
        var (poll, _) = await StartPollAsync();
        var ann = await PlayerAsync("Ann");
        var bob = await PlayerAsync("Bob");

        await VoteOkAsync(ann, poll, "Mortal Kombat");
        await VoteOkAsync(ann, poll, "UFC");
        await VoteOkAsync(bob, poll, "Mortal Kombat");

        var details = await DetailsAsync(ann);
        Votes(details).Should().BeEquivalentTo(new Dictionary<string, int> { ["Mortal Kombat"] = 1, ["UFC"] = 1 });
        details.GetProperty("myVote").GetString().Should().Be("UFC");
    }

    [Fact]
    public async Task Close_picks_majority()
    {
        var (poll, admin) = await StartPollAsync();
        await VoteOkAsync(await PlayerAsync("Ann"), poll, "UFC");
        await VoteOkAsync(await PlayerAsync("Bob"), poll, "UFC");
        await VoteOkAsync(await PlayerAsync("Cid"), poll, "Mortal Kombat");

        (await admin.PostAsync($"/api/admin/poll/{poll}/close", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(admin);
        details.GetProperty("closed").GetBoolean().Should().BeTrue();
        details.GetProperty("winner").GetString().Should().Be("UFC");
        details.GetProperty("tie").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Tie_is_broken_by_coin()
    {
        var (poll, admin) = await StartPollAsync();
        await VoteOkAsync(await PlayerAsync("Ann"), poll, "UFC");
        await VoteOkAsync(await PlayerAsync("Bob"), poll, "Mortal Kombat");

        (await admin.PostAsync($"/api/admin/poll/{poll}/close", null)).EnsureSuccessStatusCode();

        var details = await DetailsAsync(admin);
        details.GetProperty("tie").GetBoolean().Should().BeTrue();
        new[] { "UFC", "Mortal Kombat" }.Should().Contain(details.GetProperty("winner").GetString());
    }

    [Fact]
    public async Task No_votes_close_still_picks_winner()
    {
        var (poll, admin) = await StartPollAsync();

        (await admin.PostAsync($"/api/admin/poll/{poll}/close", null)).EnsureSuccessStatusCode();

        (await DetailsAsync(admin)).GetProperty("winner").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Vote_after_close_409()
    {
        var (poll, admin) = await StartPollAsync();
        var ann = await PlayerAsync("Ann");
        (await admin.PostAsync($"/api/admin/poll/{poll}/close", null)).EnsureSuccessStatusCode();

        var response = await VoteAsync(ann, poll, "UFC");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Vote_unknown_option_400()
    {
        var (poll, _) = await StartPollAsync();

        var response = await VoteAsync(await PlayerAsync("Ann"), poll, "Тетрис");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_cannot_vote()
    {
        var (poll, admin) = await StartPollAsync();

        var response = await VoteAsync(admin, poll, "UFC");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Finish_without_close_stores_winner_and_awards_nothing()
    {
        var (poll, admin) = await StartPollAsync();
        await VoteOkAsync(await PlayerAsync("Ann"), poll, "UFC");

        (await admin.PostAsync($"/api/admin/events/{poll}/finish", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = await db.Events.SingleAsync(e => e.Id == poll);
        JsonDocument.Parse(ev.ResultJson!).RootElement.GetProperty("winner").GetString().Should().Be("UFC");
        (await db.DonkeyAwards.AnyAsync(a => a.EventId == poll)).Should().BeFalse();
    }

    [Fact]
    public async Task Reset_clears_votes_after_rehearsal()
    {
        var (poll, admin) = await StartPollAsync();
        await VoteOkAsync(await PlayerAsync("Ann"), poll, "UFC");

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PollVotes.AnyAsync()).Should().BeFalse();
    }

    public void Dispose() => _factory.Dispose();

    private async Task<(Guid PollId, HttpClient Admin)> StartPollAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "FightPoll", title = "Во что рубимся?" }, TestClients.Json);
        var poll = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{poll}/start", null)).EnsureSuccessStatusCode();
        return (poll, admin);
    }

    private async Task<HttpClient> PlayerAsync(string login)
    {
        await _factory.SeedPlayerAsync(login);
        return await TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);
    }

    private static Task<HttpResponseMessage> VoteAsync(HttpClient client, Guid poll, string option) =>
        client.PostAsJsonAsync($"/api/events/{poll}/vote", new { option });

    private static async Task VoteOkAsync(HttpClient client, Guid poll, string option) =>
        (await VoteAsync(client, poll, option)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client)
    {
        var active = await client.GetFromJsonAsync<JsonElement>("/api/events/active");
        return active.GetProperty("details");
    }

    private static Dictionary<string, int> Votes(JsonElement details) =>
        details.GetProperty("options").EnumerateArray()
            .ToDictionary(o => o.GetProperty("name").GetString()!, o => o.GetProperty("votes").GetInt32());
}
