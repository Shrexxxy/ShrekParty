using System.Net.Http.Json;
using AwesomeAssertions;
using ShrekParty.Api.Live;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Pets;

public class PetSnapshotTests : IDisposable
{
    private readonly AppFactory _factory = new();
    private readonly Dictionary<string, Guid> _ids = [];

    [Fact]
    public async Task Player_sees_own_pet_only()
    {
        await StartAsync(3);

        var snapshot = await SnapshotAsync(new SnapshotAudience(SignedIn: true, ViewerId: _ids["Гость1"]));

        snapshot.MyPet!.UserId.Should().Be(_ids["Гость1"]);
        snapshot.Pets.Should().BeNull();
    }

    [Fact]
    public async Task Tv_sees_counter_without_list()
    {
        await StartAsync(10);
        foreach (var login in new[] { "Гость1", "Гость2", "Гость3" })
        {
            (await (await PlayerAsync(login)).PostAsync("/api/pets/hatch", null)).EnsureSuccessStatusCode();
        }

        var snapshot = await SnapshotAsync(new SnapshotAudience(SignedIn: true));

        snapshot.MyPet.Should().BeNull();
        snapshot.Pets!.Hatched.Should().Be(3);
        snapshot.Pets.Total.Should().Be(10);
        snapshot.Pets.Pets.Should().BeEmpty();
    }

    [Fact]
    public async Task Tv_counter_only_before_first_event()
    {
        await StartAsync(3);
        var tv = new SnapshotAudience(SignedIn: true);
        (await SnapshotAsync(tv)).Pets!.BeforeFirstEvent.Should().BeTrue();
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "FightPoll", title = "Опрос", options = new[] { "А", "Б" } }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ShrekParty.Api.Events.ProgramItemDto>(TestClients.Json))!.Id;

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();

        (await SnapshotAsync(tv)).Pets!.BeforeFirstEvent.Should().BeFalse();
    }

    [Fact]
    public async Task Admin_sees_all()
    {
        await StartAsync(10);

        var snapshot = await SnapshotAsync(new SnapshotAudience(SignedIn: true, IsAdmin: true));

        snapshot.Pets!.Pets.Should().HaveCount(10);
    }

    [Fact]
    public async Task Level_up_visible_in_snapshot()
    {
        await StartAsync(1);
        var me = new SnapshotAudience(SignedIn: true, ViewerId: _ids["Гость1"]);
        var before = (await SnapshotAsync(me)).MyPet!;
        var admin = await TestClients.AdminAsync(_factory);

        (await admin.PostAsJsonAsync("/api/admin/awards", new { userId = _ids["Гость1"], amount = 50, reason = "Ивент" })).EnsureSuccessStatusCode();

        var after = (await SnapshotAsync(me)).MyPet!;
        after.Level.Should().Be(2);
        (after.Strength + after.Agility + after.Intellect).Should().Be(before.Strength + before.Agility + before.Intellect + 3);
    }

    public void Dispose() => _factory.Dispose();

    private async Task StartAsync(int players)
    {
        for (var i = 1; i <= players; i++)
        {
            _ids[$"Гость{i}"] = await _factory.SeedPlayerAsync($"Гость{i}");
        }

        await _factory.OpenRegistrationAsync();
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { })).EnsureSuccessStatusCode();
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private async Task<AppSnapshot> SnapshotAsync(SnapshotAudience audience)
    {
        AppSnapshot? snapshot = null;
        await _factory.WithServiceAsync<SnapshotBuilder>(async b => snapshot = await b.BuildAsync(audience));
        return snapshot!;
    }
}
