using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using ShrekParty.Api.Tv;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Tv;

public class TvFactsTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Facts_empty_before_close()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya", first: "123456");

        (await FactsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Facts_contain_mask_for_weak_password()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya", first: "123456");
        await CloseAsync();

        (await FactsAsync()).Should().Contain(f => f.Text.Contains("petya") && f.Text.Contains("1*34*6"));
    }

    [Fact]
    public async Task Facts_list_mentioned_logins()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya", first: "123456");
        await CloseAsync();

        var mask = (await FactsAsync()).Single(f => f.Text.Contains("1*34*6"));

        mask.Logins.Should().Equal("petya");
    }

    [Fact]
    public async Task Facts_describe_weak_password_patterns()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya", first: "123456");
        await CloseAsync();

        (await FactsAsync()).Should().Contain(f => f.Text == "Первый пароль petya — один из самых популярных паролей в мире");
    }

    [Fact]
    public async Task Facts_never_mention_strong_first_password()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("vasya", first: RegistrationSteps.Strong, second: "Another-Strong-Pass-77!");
        await _factory.RegisterAsync("petya", first: "123456");
        await CloseAsync();

        (await FactsAsync()).Should().NotContain(f => f.Text.Contains("vasya") && f.Text.Contains("пароль"));
    }

    [Fact]
    public async Task Facts_fastest_and_slowest_player()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(10));
        await _factory.RegisterAsync("bob");
        _factory.Time.Advance(TimeSpan.FromSeconds(65));
        await _factory.RegisterAsync("ann");
        await CloseAsync();

        var facts = await FactsAsync();

        facts.Should().Contain(f => f.Text == "Быстрее всех зарегистрировался bob — 0:10");
        facts.Should().Contain(f => f.Text == "Дольше всех ловил печеньки ann — 1:15");
    }

    [Fact]
    public async Task Late_guest_is_not_slowest()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(10));
        await _factory.RegisterAsync("bob");
        _factory.Time.Advance(TimeSpan.FromSeconds(20));
        await _factory.RegisterAsync("ann");
        await CloseAsync();
        _factory.Time.Advance(TimeSpan.FromMinutes(30));
        await _factory.RegisterAsync("late");

        (await FactsAsync()).Should().NotContain(f => f.Text.Contains("late"));
    }

    [Fact]
    public async Task Player_cannot_read_facts()
    {
        await _factory.SeedPlayerAsync("petya");
        var player = await TestClients.LoggedInAsync(_factory, "petya", TestData.PlayerPassword);

        (await player.GetAsync("/api/tv/facts")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<List<FactDto>> FactsAsync()
    {
        var viewer = await TestClients.ViewerAsync(_factory);
        return (await viewer.GetFromJsonAsync<List<FactDto>>("/api/tv/facts", TestClients.Json))!;
    }

    private async Task CloseAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" })).EnsureSuccessStatusCode();
    }
}
