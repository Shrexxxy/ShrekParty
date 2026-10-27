using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Laugh;

public class LaughTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Best_of_played_games_ranks()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartAsync();
        await ScoreOkAsync(admin, id, ann, 1, 1000);
        await ScoreOkAsync(admin, id, ann, 2, 1900);
        await ScoreOkAsync(admin, id, ann, 3, 800);
        await ScoreOkAsync(admin, id, bob, 1, 1500);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        var tab = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        tab!.Select(r => (r.Login, r.Donkeys)).Should().Equal(("Ann", 100), ("Bob", 50));
    }

    [Fact]
    public async Task Player_without_games_gets_nothing()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var cid = await _factory.SeedPlayerAsync("Cid");
        var (id, admin) = await StartAsync();
        await ScoreOkAsync(admin, id, ann, 1, 500);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DonkeyAwards.AnyAsync(a => a.EventId == id && a.UserId == cid)).Should().BeFalse();
    }

    [Fact]
    public async Task Details_show_games_and_best()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartAsync();
        await ScoreOkAsync(admin, id, ann, 1, 1000);
        await ScoreOkAsync(admin, id, ann, 3, 1200);

        var rows = (await DetailsAsync(admin)).GetProperty("rows").EnumerateArray().ToList();

        var annRow = rows[0];
        annRow.GetProperty("login").GetString().Should().Be("Ann");
        annRow.GetProperty("games").EnumerateArray().Select(g => g.ValueKind == JsonValueKind.Null ? (int?)null : g.GetInt32())
            .Should().Equal(1000, null, 1200);
        annRow.GetProperty("best").GetInt32().Should().Be(1200);
        rows[1].GetProperty("best").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Update_overwrites_and_null_removes_score()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync();
        await ScoreOkAsync(admin, id, ann, 1, 1000);
        await ScoreOkAsync(admin, id, ann, 1, 1100);
        (await GamesAsync(admin))[0].Should().Be(1100);

        await ScoreOkAsync(admin, id, ann, 1, null);

        (await GamesAsync(admin))[0].Should().BeNull();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(4, 100)]
    [InlineData(1, -5)]
    public async Task Bad_game_or_points_400(int game, int points)
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync();

        var response = await ScoreAsync(admin, id, ann, game, points);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Unknown_player_404()
    {
        var (id, admin) = await StartAsync();

        var response = await ScoreAsync(admin, id, Guid.NewGuid(), 1, 100);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Score_when_not_active_409()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        var response = await ScoreAsync(admin, id, ann, 1, 100);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reset_clears_scores()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync();
        await ScoreOkAsync(admin, id, ann, 1, 100);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.LaughScores.AnyAsync()).Should().BeFalse();
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Laugh", title = "Смехлыст" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private static Task<HttpResponseMessage> ScoreAsync(HttpClient admin, Guid id, Guid userId, int game, int? points) =>
        admin.PutAsJsonAsync($"/api/admin/laugh/{id}/scores", new { userId, game, points });

    private static async Task ScoreOkAsync(HttpClient admin, Guid id, Guid userId, int game, int? points) =>
        (await ScoreAsync(admin, id, userId, game, points)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static async Task<List<int?>> GamesAsync(HttpClient admin) =>
        (await DetailsAsync(admin)).GetProperty("rows")[0].GetProperty("games").EnumerateArray()
            .Select(g => g.ValueKind == JsonValueKind.Null ? (int?)null : g.GetInt32())
            .ToList();
}
