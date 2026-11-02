using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Api.Neuro;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.NeuroPhoto;

public class NeuroPhotoTests : IDisposable
{
    private static readonly NeuroRound Pasha = new("aaa111", "Паша", "Джек Воробей");
    private static readonly NeuroRound Katya = new("bbb222", "Катя", "Соломон");

    private AppFactory _factory = Factory(Pasha, Katya);

    [Fact]
    public async Task Start_with_empty_catalog_409()
    {
        _factory.Dispose();
        _factory = Factory();
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        var response = await admin.PostAsync($"/api/admin/events/{id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("neuro_not_ready");
    }

    [Fact]
    public async Task Guessing_shows_result_people_and_characters_without_answer()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await StartAsync(admin);

        var details = await DetailsAsync(await PlayerAsync("Ann"));

        details.GetProperty("people").EnumerateArray().Select(o => o.GetString()).Should()
            .Equal("Дима", "Алеся", "Жека", "Лиза", "Леха", "Кристина", "Саня", "Артем", "Настя", "Соня", "Катя", "Паша");
        details.GetProperty("characters").EnumerateArray().Select(o => o.GetString()).Should()
            .Equal("Джек Воробей", "Джокер", "Соломон");
        details.GetProperty("photoUrl").GetString().Should().MatchRegex("^/assets/neuro/(aaa111|bbb222)-result\\.jpg$");
        details.GetProperty("answer").ValueKind.Should().Be(JsonValueKind.Null);
        details.GetProperty("character").ValueKind.Should().Be(JsonValueKind.Null);
        details.GetProperty("friendUrl").ValueKind.Should().Be(JsonValueKind.Null);
        details.GetProperty("sourceUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Guess_and_change()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);
        var player = await PlayerAsync("Ann");

        await GuessOkAsync(player, id, person: "Паша");
        await GuessOkAsync(player, id, character: "Соломон");
        await GuessOkAsync(player, id, person: "Дима");

        var details = await DetailsAsync(player);
        details.GetProperty("myPerson").GetString().Should().Be("Дима");
        details.GetProperty("myCharacter").GetString().Should().Be("Соломон");
        details.GetProperty("guessed").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Guess_unknown_name_400()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);

        var ann = await PlayerAsync("Ann");
        (await GuessAsync(ann, id, person: "Шрек")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await GuessAsync(ann, id, character: "Шрек")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Guess_outside_guessing_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);
        await AdvanceAsync(TimeSpan.FromSeconds(20));

        (await GuessAsync(await PlayerAsync("Ann"), id, person: "Паша")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cycle_rounds_with_12s_reveal_then_finish()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);

        var first = await DetailsAsync(admin);
        first.GetProperty("index").GetInt32().Should().Be(0);
        first.GetProperty("total").GetInt32().Should().Be(2);
        first.GetProperty("phase").GetString().Should().Be("Guessing");
        first.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(20));

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        var reveal = await DetailsAsync(admin);
        reveal.GetProperty("phase").GetString().Should().Be("Reveal");
        reveal.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(12));

        await AdvanceAsync(TimeSpan.FromSeconds(12));
        var second = await DetailsAsync(admin);
        second.GetProperty("index").GetInt32().Should().Be(1);
        second.GetProperty("phase").GetString().Should().Be("Guessing");

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        await AdvanceAsync(TimeSpan.FromSeconds(12));
        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
    }

    [Fact]
    public async Task Every_round_shown_once()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await StartAsync(admin);

        var first = (await DetailsAsync(admin)).GetProperty("photoUrl").GetString();
        await AdvanceAsync(TimeSpan.FromSeconds(20));
        await AdvanceAsync(TimeSpan.FromSeconds(12));
        var second = (await DetailsAsync(admin)).GetProperty("photoUrl").GetString();

        new[] { first, second }.Should().BeEquivalentTo("/assets/neuro/aaa111-result.jpg", "/assets/neuro/bbb222-result.jpg");
    }

    [Fact]
    public async Task Reveal_shows_answer_sources_and_correct()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);
        var round = RoundOf(await DetailsAsync(admin));
        await GuessOkAsync(await PlayerAsync("Ann"), id, round.Answer, round.Character);
        await GuessOkAsync(await PlayerAsync("Bob"), id, "Дима", round.Character);

        await AdvanceAsync(TimeSpan.FromSeconds(20));

        var details = await DetailsAsync(await PlayerAsync("Bob"));
        details.GetProperty("answer").GetString().Should().Be(round.Answer);
        details.GetProperty("character").GetString().Should().Be(round.Character);
        details.GetProperty("myPoints").GetInt32().Should().Be(2);
        details.GetProperty("correct").EnumerateArray().Select(c => c.GetString()).Should().Equal("Ann");
        details.GetProperty("friendUrl").GetString().Should().Be($"/assets/neuro/{round.Id}-friend.jpg");
        details.GetProperty("sourceUrl").GetString().Should().Be($"/assets/neuro/{round.Id}-source.jpg");
    }

    [Fact]
    public async Task Points_awarded_on_reveal_two_two_five()
    {
        foreach (var login in new[] { "Ann", "Bob", "Cid", "Dan" })
        {
            await _factory.SeedPlayerAsync(login);
        }

        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);
        var round = RoundOf(await DetailsAsync(admin));
        var other = round == Pasha ? Katya : Pasha;
        await GuessOkAsync(await PlayerAsync("Ann"), id, round.Answer, round.Character);
        await GuessOkAsync(await PlayerAsync("Bob"), id, round.Answer, other.Character);
        await GuessOkAsync(await PlayerAsync("Cid"), id, other.Answer, round.Character);
        await GuessOkAsync(await PlayerAsync("Dan"), id, other.Answer, other.Character);

        await AdvanceAsync(TimeSpan.FromSeconds(20));

        // Ослики — сразу на раскрытии, ивент ещё идёт.
        (await StatusAsync(id)).Should().Be(EventStatus.Active);
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 5, ["Bob"] = 2, ["Cid"] = 2, ["Dan"] = 0 });

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 5, ["Bob"] = 2, ["Cid"] = 2, ["Dan"] = 0 });
    }

    [Fact]
    public async Task Tv_slides_show_triples_only_after_game()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var tv = await TestClients.LoggedInAsync(_factory, AppFactory.ViewerLogin, AppFactory.ViewerPassword);
        var id = await StartAsync(admin);

        (await NeuroSlidesAsync(tv)).Should().BeEmpty();

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        var slides = await NeuroSlidesAsync(tv);
        slides.Should().HaveCount(2);
        var pasha = slides.Single(s => s.GetProperty("text").GetString() == "Это Паша в образе: Джек Воробей!");
        pasha.GetProperty("images").EnumerateArray().Select(i => i.GetString()).Should().Equal(
            "/assets/neuro/aaa111-friend.jpg", "/assets/neuro/aaa111-source.jpg", "/assets/neuro/aaa111-result.jpg");
    }

    [Fact]
    public async Task Reset_clears_guesses()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin);
        await GuessOkAsync(await PlayerAsync("Ann"), id, person: "Паша");

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().NeuroGuesses.CountAsync()).Should().Be(0);
    }

    public void Dispose() => _factory.Dispose();

    private static AppFactory Factory(params NeuroRound[] rounds) =>
        new(configureServices: s => s.Replace(ServiceDescriptor.Singleton(new NeuroCatalog(rounds, ["Джокер"]))));

    private static NeuroRound RoundOf(JsonElement details) =>
        details.GetProperty("photoUrl").GetString()!.Contains(Pasha.Id) ? Pasha : Katya;

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "NeuroPhoto", title = "Нейрофото" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private static async Task<Guid> StartAsync(HttpClient admin)
    {
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return id;
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> GuessAsync(HttpClient player, Guid id, string? person = null, string? character = null) =>
        player.PostAsJsonAsync($"/api/neuro/{id}/guess", new { person, character });

    private static async Task GuessOkAsync(HttpClient player, Guid id, string? person = null, string? character = null) =>
        (await GuessAsync(player, id, person, character)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static async Task<List<JsonElement>> NeuroSlidesAsync(HttpClient tv) =>
        (await tv.GetFromJsonAsync<List<JsonElement>>("/api/tv/slides"))!.Where(s => s.GetProperty("kind").GetString() == "Neuro").ToList();

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();

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
