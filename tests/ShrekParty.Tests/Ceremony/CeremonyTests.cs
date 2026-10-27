using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.Reaction;
using ShrekParty.Api.Tapper;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Ceremony;

public class CeremonyTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Standings_frozen_at_start()
    {
        await PlayerWithAsync("Ann", 150);
        var bob = await PlayerWithAsync("Bob", 100);
        var (_, admin) = await StartCeremonyAsync();

        await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(bob, 500, "После церемонии"));

        var standings = (await DetailsAsync(admin)).GetProperty("standings").EnumerateArray().ToList();
        standings.Select(s => (s.GetProperty("login").GetString(), s.GetProperty("donkeys").GetInt32(), s.GetProperty("place").GetInt32()))
            .Should().Equal(("Ann", 150, 1), ("Bob", 100, 2));
    }

    [Fact]
    public async Task Next_advances_through_all_steps_then_409()
    {
        await PlayerWithAsync("Ann", 150);
        await PlayerWithAsync("Bob", 100);
        var (id, admin) = await StartCeremonyAsync();
        var details = await DetailsAsync(admin);
        details.GetProperty("step").GetInt32().Should().Be(0);
        var total = details.GetProperty("totalSteps").GetInt32();
        total.Should().Be(5);

        for (var i = 1; i < total; i++)
        {
            (await admin.PostAsync($"/api/admin/ceremony/{id}/next", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await DetailsAsync(admin)).GetProperty("step").GetInt32().Should().Be(total - 1);
        var extra = await admin.PostAsync($"/api/admin/ceremony/{id}/next", null);
        extra.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Achievements_come_from_events_that_happened()
    {
        var ann = await PlayerWithAsync("Ann", 150);
        var bob = await PlayerWithAsync("Bob", 150);
        var admin = await TestClients.AdminAsync(_factory);
        var laugh = await CreateAsync(admin, "Laugh", "Смехлыст");
        (await admin.PostAsync($"/api/admin/events/{laugh}/start", null)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/laugh/{laugh}/scores", new { userId = ann, game = 1, points = 3000 })).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/api/admin/laugh/{laugh}/scores", new { userId = bob, game = 1, points = 1000 })).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/events/{laugh}/finish", null)).EnsureSuccessStatusCode();

        await StartCeremonyAsync(admin);

        var achievements = Achievements(await DetailsAsync(admin));
        achievements["Ann"].Should().Be("Главный юморист");
        achievements["Bob"].Should().Be("Просто хороший огр");
    }

    [Fact]
    public async Task Invisible_man_only_among_hide_players()
    {
        var ann = await PlayerWithAsync("Ann", 150);
        var bob = await PlayerWithAsync("Bob", 150);
        var admin = await TestClients.AdminAsync(_factory);
        var hide = await CreateAsync(admin, "HideAndSeek", "Прятки");
        (await admin.PostAsync($"/api/admin/events/{hide}/start", null)).EnsureSuccessStatusCode();
        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
        await ShotAsync(admin, hide, ann, bob);
        await ShotAsync(admin, hide, ann);
        (await admin.PostAsync($"/api/admin/events/{hide}/finish", null)).EnsureSuccessStatusCode();
        // Пришёл после пряток — ни разу не попался, но и не прятался.
        await PlayerWithAsync("Cid", 150);

        await StartCeremonyAsync(admin);

        var achievements = Achievements(await DetailsAsync(admin));
        achievements["Bob"].Should().Be("Человек-невидимка");
        achievements["Cid"].Should().NotBe("Человек-невидимка");
    }

    [Fact]
    public async Task Achievements_tapper_and_reaction()
    {
        var ann = await PlayerWithAsync("Ann", 150);
        var bob = await PlayerWithAsync("Bob", 150);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ev = Guid.NewGuid();
            db.TapScores.AddRange(
                new TapScore { Id = Guid.NewGuid(), EventId = ev, UserId = ann, Taps = 300 },
                new TapScore { Id = Guid.NewGuid(), EventId = ev, UserId = bob, Taps = 100 });
            db.ReactionAttempts.AddRange(
                new ReactionAttempt { Id = Guid.NewGuid(), EventId = ev, UserId = ann, Round = 1, Ms = 400 },
                new ReactionAttempt { Id = Guid.NewGuid(), EventId = ev, UserId = bob, Round = 1, Ms = 150 },
                new ReactionAttempt { Id = Guid.NewGuid(), EventId = ev, UserId = ann, Round = 2, Ms = null });
            await db.SaveChangesAsync();
        }

        var (_, admin) = await StartCeremonyAsync();

        var achievements = Achievements(await DetailsAsync(admin));
        achievements["Ann"].Should().Be("Король тапалки");
        achievements["Bob"].Should().Be("Быстрее пули");
    }

    [Fact]
    public async Task Tote_net_gives_bettor_and_bankrupt()
    {
        var ann = await PlayerWithAsync("Ann", 150);
        var bob = await PlayerWithAsync("Bob", 150);
        await AwardBetAsync(ann, 20);
        await AwardBetAsync(bob, -10);

        var (_, admin) = await StartCeremonyAsync();

        var achievements = Achievements(await DetailsAsync(admin));
        achievements["Ann"].Should().Be("Ставочник года");
        achievements["Bob"].Should().Be("Банкрот");
    }

    [Fact]
    public async Task Ceremony_awards_nothing()
    {
        await PlayerWithAsync("Ann", 150);
        var (id, admin) = await StartCeremonyAsync();

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        var total = (await admin.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json))!.Single().Donkeys;
        total.Should().Be(150);
    }

    [Fact]
    public async Task Next_when_not_active_409()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin, "Ceremony", "Церемония");

        (await admin.PostAsync($"/api/admin/ceremony/{id}/next", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> PlayerWithAsync(string login, int donkeys)
    {
        var id = await _factory.SeedPlayerAsync(login);
        await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(id, donkeys, DonkeyReasons.StartCapital));
        return id;
    }

    private async Task AwardBetAsync(Guid userId, int amount)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.DonkeyAwards.Add(new DonkeyAward { Id = Guid.NewGuid(), UserId = userId, BetId = Guid.NewGuid(), Amount = amount, Reason = "Ставка", CreatedAt = _factory.Time.GetUtcNow() });
        await db.SaveChangesAsync();
    }

    private static async Task ShotAsync(HttpClient admin, Guid hideId, params Guid[] userIds)
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var form = new MultipartFormDataContent { { file, "file", "shot.jpg" }, { new StringContent(string.Join(',', userIds)), "userIds" } };
        (await admin.PostAsync($"/api/admin/hide/{hideId}/shots", form)).EnsureSuccessStatusCode();
    }

    private async Task<(Guid Id, HttpClient Admin)> StartCeremonyAsync(HttpClient? admin = null)
    {
        admin ??= await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin, "Ceremony", "Церемония");
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, string type, string title)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type, title }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static Dictionary<string, string> Achievements(JsonElement details) =>
        details.GetProperty("standings").EnumerateArray()
            .ToDictionary(s => s.GetProperty("login").GetString()!, s => s.GetProperty("achievement").GetString()!);
}
