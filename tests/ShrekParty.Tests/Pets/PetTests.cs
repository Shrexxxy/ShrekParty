using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Auction;
using ShrekParty.Api.Data;
using ShrekParty.Api.Pets;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Pets;

public class PetTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Close_creates_pets_balanced()
    {
        var logins = Enumerable.Range(1, 10).Select(i => $"Гость{i}").ToArray();
        await SeedAsync(logins);

        await CloseRegistrationAsync();

        var pets = await AllAsync();
        pets.Should().HaveCount(10);
        pets.GroupBy(p => p.Class).Select(g => g.Count()).Order().Should().Equal(3, 3, 4);
        pets.Should().OnlyContain(p => !p.Hatched && p.Level == 1 && p.Name == $"Дракоша {p.Login}");
    }

    [Fact]
    public async Task Late_player_gets_least_class()
    {
        await SeedAsync("Ann", "Bob");
        await CloseRegistrationAsync();
        var taken = (await AllAsync()).Select(p => p.Class).ToList();

        await _factory.RegisterAsync("Опоздун");

        var late = (await AllAsync()).Single(p => p.Login == "Опоздун");
        taken.Should().NotContain(late.Class);

        // Стартовый капитал — не заработок: питомец начинает с 1-го уровня.
        late.Earned.Should().Be(0);
        late.Level.Should().Be(1);
    }

    [Fact]
    public async Task Open_registration_no_pet_yet()
    {
        await _factory.OpenRegistrationAsync();
        var client = await _factory.RegisterAsync("Ранний");

        (await client.PostAsync("/api/pets/hatch", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Ensure_twice_no_duplicates()
    {
        await SeedAsync("Ann", "Bob", "Cid");
        await CloseRegistrationAsync();
        var before = await AllAsync();

        await _factory.WithServiceAsync<PetService>(p => p.EnsureAsync());

        (await AllAsync()).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task Ensure_in_parallel_no_errors()
    {
        await SeedAsync(Enumerable.Range(1, 8).Select(i => $"Гость{i}").ToArray());

        // Двое опоздавших дорегистрировались в одну секунду — оба вызова создания яиц идут разом.
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _factory.WithServiceAsync<PetService>(p => p.EnsureAsync())));

        var pets = await AllAsync();
        pets.Should().HaveCount(8);
        pets.GroupBy(p => p.Class).Select(g => g.Count()).Order().Should().Equal(2, 3, 3);
    }

    [Fact]
    public async Task Hatch_once()
    {
        await SeedAsync("Ann");
        await CloseRegistrationAsync();
        var player = await PlayerAsync("Ann");
        var egg = (await AllAsync()).Single();

        (await player.PostAsync("/api/pets/hatch", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await player.PostAsync("/api/pets/hatch", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pet = (await AllAsync()).Single();
        pet.Hatched.Should().BeTrue();
        pet.Should().BeEquivalentTo(egg with { Hatched = true });
    }

    [Fact]
    public async Task Player_names_once_after_hatch()
    {
        await SeedAsync("Ann");
        await CloseRegistrationAsync();
        var ann = await PlayerAsync("Ann");
        (await ann.PostAsync("/api/pets/hatch", null)).EnsureSuccessStatusCode();

        (await ann.PutAsJsonAsync("/api/pets/name", new { name = "   " })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ann.PutAsJsonAsync("/api/pets/name", new { name = new string('я', 21) })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AllAsync()).Single().Named.Should().BeFalse();

        (await ann.PutAsJsonAsync("/api/pets/name", new { name = "  Искорка  " })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var again = await ann.PutAsJsonAsync("/api/pets/name", new { name = "Огонёк" });

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(again)).Should().Be("already_named");
        var pet = (await AllAsync()).Single();
        pet.Name.Should().Be("Искорка");
        pet.Named.Should().BeTrue();
    }

    [Fact]
    public async Task Player_cannot_name_egg()
    {
        await SeedAsync("Ann");
        await CloseRegistrationAsync();
        var ann = await PlayerAsync("Ann");

        var response = await ann.PutAsJsonAsync("/api/pets/name", new { name = "Искорка" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("not_hatched");
        (await NameOfAsync("Ann")).Should().Be("Дракоша Ann");
    }

    [Fact]
    public async Task Admin_renames_any_time_player_still_locked()
    {
        await SeedAsync("Ann");
        await CloseRegistrationAsync();
        var ann = await PlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        (await ann.PostAsync("/api/pets/hatch", null)).EnsureSuccessStatusCode();
        (await ann.PutAsJsonAsync("/api/pets/name", new { name = "Искорка" })).EnsureSuccessStatusCode();
        var id = await IdAsync("Ann");

        (await admin.PutAsJsonAsync($"/api/admin/pets/{id}/name", new { name = "Пушок" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await NameOfAsync("Ann")).Should().Be("Пушок");
        (await ann.PutAsJsonAsync("/api/pets/name", new { name = "Огонёк" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Variants_unique_within_class_while_possible()
    {
        await SeedAsync(Enumerable.Range(1, 15).Select(i => $"Гость{i}").ToArray());

        await CloseRegistrationAsync();

        var pets = await AllAsync();
        pets.GroupBy(p => p.Class).Should().OnlyContain(g => g.Select(p => p.Variant).Order().SequenceEqual(new[] { 1, 2, 3, 4, 5 }));

        await _factory.RegisterAsync("Опоздун");

        (await AllAsync()).Single(p => p.Login == "Опоздун").Variant.Should().BeInRange(1, 5);
    }

    [Fact]
    public async Task Admin_renames_egg()
    {
        await SeedAsync("Ann", "Bob");
        await CloseRegistrationAsync();
        var ann = await PlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);

        var bob = await IdAsync("Bob");
        (await admin.PutAsJsonAsync($"/api/admin/pets/{bob}/name", new { name = "Пушок" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await NameOfAsync("Bob")).Should().Be("Пушок");
        (await ann.PutAsJsonAsync($"/api/admin/pets/{bob}/name", new { name = "Чужой" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Level_from_positive_awards_only()
    {
        await SeedAsync("Ann");
        await CloseRegistrationAsync();
        var ann = await IdAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/awards", new { userId = ann, amount = 60, reason = "Ивент" })).EnsureSuccessStatusCode();
        (await admin.PostAsJsonAsync("/api/admin/awards", new { userId = ann, amount = -25, reason = "Штраф" })).EnsureSuccessStatusCode();
        await SeedSaleAsync(ann, 30);

        var pet = (await AllAsync()).Single();

        pet.Earned.Should().Be(60);
        pet.Level.Should().Be(2);
        pet.NextLevelAt.Should().Be(100);
    }

    [Fact]
    public async Task Reset_removes_pets()
    {
        await SeedAsync("Ann");
        await CloseRegistrationAsync();
        var admin = await TestClients.AdminAsync(_factory);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        (await AllAsync()).Should().BeEmpty();
        using var scope = _factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Pets.Should().BeEmpty();
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(params string[] logins)
    {
        foreach (var login in logins)
        {
            await _factory.SeedPlayerAsync(login);
        }
    }

    private async Task CloseRegistrationAsync()
    {
        await _factory.OpenRegistrationAsync();
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { })).EnsureSuccessStatusCode();
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private async Task<IReadOnlyList<PetDto>> AllAsync()
    {
        IReadOnlyList<PetDto> pets = [];
        await _factory.WithServiceAsync<PetService>(async p => pets = await p.AllAsync());
        return pets;
    }

    private async Task<Guid> IdAsync(string login) => (await AllAsync()).Single(p => p.Login == login).UserId;

    private async Task<string> NameOfAsync(string login) => (await AllAsync()).Single(p => p.Login == login).Name;

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("error").GetString();

    private async Task SeedSaleAsync(Guid userId, int price)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.AuctionSales.Add(new AuctionSale { Id = Guid.NewGuid(), EventId = Guid.NewGuid(), LotId = Guid.NewGuid(), UserId = userId, Price = price, CreatedAt = _factory.Time.GetUtcNow() });
        await db.SaveChangesAsync();
    }
}
