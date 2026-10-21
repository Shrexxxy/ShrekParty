using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Scoring;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Donkeys;

public class LeaderboardTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Leaderboard_sums_awards_and_ranks_densely()
    {
        var a = await _factory.SeedPlayerAsync("A");
        var b = await _factory.SeedPlayerAsync("B");
        var c = await _factory.SeedPlayerAsync("C");
        await _factory.WithServiceAsync<DonkeyService>(async s =>
        {
            await s.AwardAsync(a, 150, "Стартовый капитал");
            await s.AwardAsync(a, 10, "тест");
            await s.AwardAsync(b, 150, "Стартовый капитал");
            await s.AwardAsync(b, 10, "тест");
            await s.AwardAsync(c, 150, "Стартовый капитал");
        });

        var rows = await GetLeaderboardAsync(await TestClients.AdminAsync(_factory));

        rows.Select(r => (r.Login, r.Donkeys, r.Place)).Should().Equal(
            ("A", 160, 1), ("B", 160, 1), ("C", 150, 2));
    }

    [Fact]
    public async Task Leaderboard_excludes_admin_and_viewer()
    {
        await _factory.SeedPlayerAsync("A");

        var rows = await GetLeaderboardAsync(await TestClients.AdminAsync(_factory));

        rows.Select(r => r.Login).Should().Equal("A");
    }

    [Fact]
    public async Task Leaderboard_filtered_by_event()
    {
        var a = await _factory.SeedPlayerAsync("A");
        var ev = await _factory.SeedEventAsync("Тестовый ивент");
        await _factory.WithServiceAsync<DonkeyService>(async s =>
        {
            await s.AwardAsync(a, 7, "тест", ev);
            await s.AwardAsync(a, 150, "Стартовый капитал");
        });

        var rows = await GetLeaderboardAsync(await TestClients.AdminAsync(_factory), ev);

        rows.Single().Donkeys.Should().Be(7);
    }

    [Fact]
    public async Task Admin_manual_award_negative()
    {
        var a = await _factory.SeedPlayerAsync("A");
        await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(a, 150, "Стартовый капитал"));
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/admin/awards", new { userId = a, amount = -20, reason = "штраф" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetLeaderboardAsync(admin)).Single().Donkeys.Should().Be(130);
    }

    [Fact]
    public async Task Manual_award_to_unknown_player_returns_404()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/admin/awards", new { userId = Guid.NewGuid(), amount = 5, reason = "тест" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Player_cannot_award()
    {
        var a = await _factory.SeedPlayerAsync("A");
        var player = await TestClients.LoggedInAsync(_factory, "A", TestData.PlayerPassword);

        var response = await player.PostAsJsonAsync("/api/admin/awards", new { userId = a, amount = 100, reason = "хитрость" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AwardRanking_linear_max_10()
    {
        var a = await _factory.SeedPlayerAsync("A");
        var b = await _factory.SeedPlayerAsync("B");
        var c = await _factory.SeedPlayerAsync("C");
        var ev = await _factory.SeedEventAsync("Тестовый ивент");
        Ranked<Guid>[] ranking = [new(a, 1), new(b, 1), new(c, 2)];

        await _factory.WithServiceAsync<DonkeyService>(s =>
            s.AwardRankingAsync(ev, ranking, ScoringScheme.Linear, 10, "тест"));

        var rows = await GetLeaderboardAsync(await TestClients.AdminAsync(_factory), ev);
        rows.Select(r => (r.Login, r.Donkeys)).Should().Equal(("A", 10), ("B", 10), ("C", 5));
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<List<LeaderboardRowDto>> GetLeaderboardAsync(HttpClient client, Guid? eventId = null)
    {
        var url = eventId is null ? "/api/leaderboard" : $"/api/leaderboard?eventId={eventId}";
        return (await client.GetFromJsonAsync<List<LeaderboardRowDto>>(url, TestClients.Json))!;
    }
}
