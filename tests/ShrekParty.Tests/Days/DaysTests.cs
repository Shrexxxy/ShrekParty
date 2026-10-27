using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Days;

public class DaysTests : IDisposable
{
    // 06.11.2001 → 06.11.2026: 25 лет, из них 6 високосных.
    private const int Truth = 25 * 365 + 6;

    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Truth_from_config()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (_, admin) = await StartAsync();

        var details = await DetailsAsync(admin);

        details.GetProperty("truth").GetInt32().Should().Be(Truth);
        details.GetProperty("phase").GetString().Should().Be("Answering");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(30));
    }

    [Fact]
    public async Task Answer_and_change()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, _) = await StartAsync();
        var ann = await PlayerAsync("Ann");

        await AnswerOkAsync(ann, id, 9000);
        await AnswerOkAsync(ann, id, 9100);

        var details = await DetailsAsync(ann);
        details.GetProperty("myAnswer").GetInt32().Should().Be(9100);
        details.GetProperty("answered").GetInt32().Should().Be(1);
        details.GetProperty("total").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Answer_after_deadline_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(30));

        (await AnswerAsync(await PlayerAsync("Ann"), id, 9000)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Negative_answer_400()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync();

        (await AnswerAsync(await PlayerAsync("Ann"), id, -1)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cheat_disqualifies_and_blocks_answer()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync();
        var ann = await PlayerAsync("Ann");
        await AnswerOkAsync(ann, id, Truth);

        (await ann.PostAsync($"/api/days/{id}/cheat", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await AnswerAsync(ann, id, Truth)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailsAsync(ann)).GetProperty("iAmDisqualified").GetBoolean().Should().BeTrue();
        (await DetailsAsync(admin)).GetProperty("disqualified").EnumerateArray().Select(l => l.GetString()).Should().Equal("Ann");
    }

    [Fact]
    public async Task Truth_hidden_until_reveal()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync();
        var ann = await PlayerAsync("Ann");
        await AnswerOkAsync(ann, id, 9000);

        var answering = await DetailsAsync(ann);
        answering.GetProperty("truth").ValueKind.Should().Be(JsonValueKind.Null);
        answering.GetProperty("answers").GetArrayLength().Should().Be(0);

        await AdvanceAsync(TimeSpan.FromSeconds(30));

        var reveal = await DetailsAsync(ann);
        reveal.GetProperty("phase").GetString().Should().Be("Reveal");
        reveal.GetProperty("truth").GetInt32().Should().Be(Truth);
    }

    [Fact]
    public async Task Reveal_lists_answers_far_to_near()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        var (id, admin) = await StartAsync();
        await AnswerOkAsync(await PlayerAsync("Ann"), id, Truth - 10);
        await AnswerOkAsync(await PlayerAsync("Bob"), id, Truth + 500);
        await AnswerOkAsync(await PlayerAsync("Cid"), id, Truth);

        await AdvanceAsync(TimeSpan.FromSeconds(30));

        var answers = (await DetailsAsync(admin)).GetProperty("answers").EnumerateArray().ToList();
        answers.Select(a => a.GetProperty("login").GetString()).Should().Equal("Bob", "Ann", "Cid");
        answers.Select(a => a.GetProperty("diff").GetInt32()).Should().Equal(500, 10, 0);
    }

    [Fact]
    public async Task Rank_by_distance_excludes_cheaters()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        await _factory.SeedPlayerAsync("Dan");
        var (id, admin) = await StartAsync();
        await AnswerOkAsync(await PlayerAsync("Ann"), id, Truth - 10);
        await AnswerOkAsync(await PlayerAsync("Bob"), id, Truth + 10);
        var cid = await PlayerAsync("Cid");
        await AnswerOkAsync(cid, id, Truth);
        await cid.PostAsync($"/api/days/{id}/cheat", null);
        await AnswerOkAsync(await PlayerAsync("Dan"), id, Truth + 100);
        await AdvanceAsync(TimeSpan.FromSeconds(30));

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        // Ann и Bob одинаково близко — общее 1-е; Dan — 2-е; Cid дисквалифицирован.
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 30, ["Bob"] = 30, ["Dan"] = 15, ["Cid"] = 0 });
    }

    public void Dispose() => _factory.Dispose();

    private async Task<(Guid Id, HttpClient Admin)> StartAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Days", title = "Сколько мне дней" }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient player, Guid id, int days) =>
        player.PostAsJsonAsync($"/api/days/{id}/answer", new { days });

    private static async Task AnswerOkAsync(HttpClient player, Guid id, int days) =>
        (await AnswerAsync(player, id, days)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid id)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }
}
