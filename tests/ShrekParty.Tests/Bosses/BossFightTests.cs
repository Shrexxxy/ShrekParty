using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Events;
using ShrekParty.Api.Pets;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Bosses;

public class BossFightTests : IDisposable
{
    private static readonly string[] Six = ["Ann", "Bob", "Cid", "Dan", "Eve", "Fox"];

    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_needs_boss_setting()
    {
        await PlayersAsync();
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin, boss: null);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Start_hatches_eggs_and_takes_all_pets()
    {
        await PlayersAsync();
        foreach (var login in Six.Take(4))
        {
            (await (await PlayerAsync(login)).PostAsync("/api/pets/hatch", null)).EnsureSuccessStatusCode();
        }

        var (_, admin) = await StartAsync();

        IReadOnlyList<PetDto> pets = [];
        await _factory.WithServiceAsync<PetService>(async p => pets = await p.AllAsync());
        pets.Should().OnlyContain(p => p.Hatched);
        var fighters = (await DetailsAsync(admin)).GetProperty("fighters").EnumerateArray().ToList();
        fighters.Should().HaveCount(6);
        foreach (var f in fighters)
        {
            var pet = pets.Single(p => p.UserId == f.GetProperty("userId").GetGuid());
            f.GetProperty("hp").GetInt32().Should().Be(pet.Health);
            f.GetProperty("maxHp").GetInt32().Should().Be(pet.Health);
            f.GetProperty("mana").GetInt32().Should().Be(pet.Mana);
        }

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Intro");
        details.GetProperty("bossHp").GetInt32().Should().Be(6 * pets.Sum(p => p.Damage));
    }

    [Fact]
    public async Task Intro_then_choosing_with_intent()
    {
        await PlayersAsync();
        var (_, admin) = await StartAsync();

        await AdvanceAsync(TimeSpan.FromSeconds(8));

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Choosing");
        details.GetProperty("turn").GetInt32().Should().Be(1);
        details.GetProperty("intent").GetProperty("move").GetString().Should().NotBeNullOrEmpty();
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(20));
    }

    [Fact]
    public async Task Choosing_ends_by_timer_then_showing_then_next_turn()
    {
        await PlayersAsync();
        var (_, admin) = await StartAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(8));

        await AdvanceAsync(TimeSpan.FromSeconds(20));

        var showing = await DetailsAsync(admin);
        showing.GetProperty("phase").GetString().Should().Be("Showing");
        var events = showing.GetProperty("feed").GetArrayLength();
        events.Should().BeGreaterThan(0);
        var playback = TimeSpan.FromMilliseconds(Math.Max(3000, 600 * events + 1000));
        showing.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow() + playback);

        await AdvanceAsync(playback);

        var next = await DetailsAsync(admin);
        next.GetProperty("phase").GetString().Should().Be("Choosing");
        next.GetProperty("turn").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Ends_won_with_medal()
    {
        await PlayersAsync();
        var (id, admin) = await StartAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(8));
        await PatchStateAsync(id, s => s["bossHp"] = 1);

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        await AdvanceAsync(TimeSpan.FromSeconds(30));

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Won");
        details.GetProperty("medal").GetString().Should().Be("Gold");
        details.GetProperty("deadline").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Ends_lost_when_all_out()
    {
        await PlayersAsync();
        var (id, admin) = await StartAsync("Farquaad");
        await AdvanceAsync(TimeSpan.FromSeconds(8));
        await PatchStateAsync(id, s =>
        {
            // Все на последнем издыхании, залп по всем, босс неубиваемый.
            s["bossHp"] = 100_000;
            s["bossMax"] = 100_000;
            foreach (var f in s["fighters"]!.AsArray())
            {
                f!["hp"] = 1;
            }

            s["intent"] = new JsonObject
            {
                ["move"] = (int)ShrekParty.Api.Bosses.BossMoveKind.Volley,
                ["targets"] = new JsonArray(s["fighters"]!.AsArray().Select(f => (JsonNode)f!["userId"]!.GetValue<Guid>().ToString()).ToArray()),
            };
        });

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        await AdvanceAsync(TimeSpan.FromSeconds(30));

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Lost");
        details.GetProperty("medal").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Rank_empty()
    {
        await PlayersAsync();
        var (id, admin) = await StartAsync();

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().DonkeyAwards.Where(a => a.EventId == id).Should().BeEmpty();
    }

    #region Действия игроков и пульт

    [Fact]
    public async Task Act_and_change_until_end()
    {
        await PlayersAsync();
        var (id, admin) = await ToChoosingAsync();

        await ActOkAsync("Ann", id, "Attack");
        await ActOkAsync("Ann", id, "Dodge");

        var seenByBob = await DetailsAsync(await PlayerAsync("Bob"));
        Fighter(seenByBob, "Ann").GetProperty("choice").GetString().Should().Be("Dodge");
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Choosing");
    }

    [Fact]
    public async Task All_alive_chose_ends_turn_early()
    {
        await PlayersAsync();
        var (id, admin) = await ToChoosingAsync();

        foreach (var login in Six)
        {
            await ActOkAsync(login, id, "Attack");
        }

        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Showing");
    }

    [Fact]
    public async Task Rules_errors()
    {
        await PlayersAsync();
        var (id, admin) = await ToChoosingAsync();
        var fighters = (await DetailsAsync(admin)).GetProperty("fighters").EnumerateArray().ToList();
        var notTank = Login(fighters.First(f => f.GetProperty("class").GetString() != "Tank"));
        var mage = Login(fighters.First(f => f.GetProperty("class").GetString() == "Mage"));
        var other = Login(fighters.First(f => Login(f) != mage));
        var dodger = Login(fighters.First(f => Login(f) != mage && Login(f) != other));
        var deadOne = Login(fighters.First(f => Login(f) != mage && Login(f) != other && Login(f) != dodger));
        await PatchStateAsync(id, s =>
        {
            FighterNode(s, Id(fighters, mage))["mana"] = 0;
            FighterNode(s, Id(fighters, dodger))["dodgedLast"] = true;
            FighterNode(s, Id(fighters, deadOne))["hp"] = 0;
        });

        (await ActAsync(notTank, id, "Cover")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ActAsync(mage, id, "Fire")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ActAsync(dodger, id, "Dodge")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ActAsync(deadOne, id, "Attack")).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await PatchStateAsync(id, s => FighterNode(s, Id(fighters, mage))["mana"] = 10);
        (await ActAsync(mage, id, "Heal", Id(fighters, deadOne))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ActAsync(mage, id, "Heal")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ActAsync(mage, id, "Heal", Id(fighters, other))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Not_choosing_rejected()
    {
        await PlayersAsync();
        var (id, _) = await StartAsync();

        (await ActAsync("Ann", id, "Attack")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Viewer_without_pet_forbidden()
    {
        await PlayersAsync();
        var (id, _) = await ToChoosingAsync();
        var late = await _factory.RegisterAsync("Опоздун");

        (await late.PostAsJsonAsync($"/api/boss/{id}/act", new { action = "Attack" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await DetailsAsync(late)).GetProperty("myOptions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Cheer_once_per_turn_bonus_next_turn()
    {
        await PlayersAsync();
        var (id, admin) = await ToChoosingAsync();
        var fighters = (await DetailsAsync(admin)).GetProperty("fighters").EnumerateArray().ToList();
        await PatchStateAsync(id, s =>
        {
            FighterNode(s, Id(fighters, "Eve"))["hp"] = 0;
            FighterNode(s, Id(fighters, "Fox"))["hp"] = 0;
        });

        (await CheerAsync("Eve", id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CheerAsync("Fox", id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CheerAsync("Eve", id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await CheerAsync("Ann", id)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        (await DetailsAsync(admin)).GetProperty("cheerBonus").GetInt32().Should().Be(2);
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        await PatchStateAsync(id, s =>
        {
            // Ann мог выбыть в первом ходу — возвращаем, чтобы проверить прибавку к удару.
            s["bossHp"] = 100_000;
            FighterNode(s, Id(fighters, "Ann"))["hp"] = 10_000;
        });
        await ActOkAsync("Ann", id, "Attack");
        await AdvanceAsync(TimeSpan.FromSeconds(20));

        IReadOnlyList<PetDto> pets = [];
        await _factory.WithServiceAsync<PetService>(async p => pets = await p.AllAsync());
        var ann = Id(fighters, "Ann");
        var hit = (await DetailsAsync(admin)).GetProperty("feed").EnumerateArray()
            .Single(f => f.GetProperty("kind").GetString() == "Attack" && f.GetProperty("actorId").GetGuid() == ann);
        hit.GetProperty("amount").GetInt32().Should().Be(pets.Single(p => p.UserId == ann).Damage + 2);
    }

    [Fact]
    public async Task Admin_next_with_stale_step_conflicts()
    {
        await PlayersAsync();
        var (id, admin) = await StartAsync();
        var step = (await DetailsAsync(admin)).GetProperty("step").GetInt32();
        await AdvanceAsync(TimeSpan.FromSeconds(8));

        (await admin.PostAsync($"/api/admin/boss/{id}/next?step={step}", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var choosing = await DetailsAsync(admin);
        (await admin.PostAsync($"/api/admin/boss/{id}/next?step={choosing.GetProperty("step").GetInt32()}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Showing");
    }

    [Fact]
    public async Task Player_cannot_next()
    {
        await PlayersAsync();
        var (id, _) = await StartAsync();

        (await (await PlayerAsync("Ann")).PostAsync($"/api/admin/boss/{id}/next", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    public void Dispose() => _factory.Dispose();

    private async Task PlayersAsync()
    {
        foreach (var login in Six)
        {
            await _factory.SeedPlayerAsync(login);
        }

        await _factory.OpenRegistrationAsync();
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { })).EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, string? boss)
    {
        object body = boss is null
            ? new { type = "BossFight", title = "Битва" }
            : new { type = "BossFight", title = "Битва", settings = new { boss } };
        var created = await admin.PostAsJsonAsync("/api/admin/events", body, TestClients.Json);
        created.EnsureSuccessStatusCode();
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartAsync(string boss = "Peasant")
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin, boss);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> ToChoosingAsync()
    {
        var started = await StartAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(8));
        return started;
    }

    private async Task<HttpResponseMessage> ActAsync(string login, Guid id, string action, Guid? targetId = null) =>
        await (await PlayerAsync(login)).PostAsJsonAsync($"/api/boss/{id}/act", new { action, targetId });

    private async Task ActOkAsync(string login, Guid id, string action) =>
        (await ActAsync(login, id, action)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task<HttpResponseMessage> CheerAsync(string login, Guid id) =>
        await (await PlayerAsync(login)).PostAsync($"/api/boss/{id}/cheer", null);

    private static string Login(JsonElement fighter) => fighter.GetProperty("login").GetString()!;

    private static Guid Id(IEnumerable<JsonElement> fighters, string login) => fighters.Single(f => Login(f) == login).GetProperty("userId").GetGuid();

    private static JsonElement Fighter(JsonElement details, string login) => details.GetProperty("fighters").EnumerateArray().Single(f => Login(f) == login);

    private static JsonObject FighterNode(JsonObject state, Guid id) =>
        state["fighters"]!.AsArray().Single(f => f!["userId"]!.GetValue<Guid>() == id)!.AsObject();

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private async Task PatchStateAsync(Guid id, Action<JsonObject> patch)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ev = await db.Events.FindAsync(id);
        var state = JsonNode.Parse(ev!.StateJson!)!.AsObject();
        patch(state);
        ev.StateJson = state.ToJsonString();
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");
}
