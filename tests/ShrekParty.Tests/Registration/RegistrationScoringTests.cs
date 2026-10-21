using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Donkeys;
using ShrekParty.Tests.Infrastructure;
using static ShrekParty.Tests.Infrastructure.RegistrationSteps;

namespace ShrekParty.Tests.Registration;

public class RegistrationScoringTests : IDisposable
{
    /// <summary>Сложный пароль (score ≥ 3), но слабее <see cref="RegistrationSteps.Strong"/>.</summary>
    private const string Medium = "Correct-Horse-7";

    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Speed_ranked_by_completion_time()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(10));
        await _factory.RegisterAsync("Bob");
        await _factory.RegisterAsync("Cid");
        _factory.Time.Advance(TimeSpan.FromSeconds(20));
        await _factory.RegisterAsync("Ann");

        await CloseAsync();

        var speed = await AwardsByReasonAsync(DonkeyReasons.RegistrationSpeed);
        speed.Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 5, ["Bob"] = 10, ["Cid"] = 10 });
    }

    [Fact]
    public async Task Password_ranked_by_strength()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("Ann", second: Strong);
        await _factory.RegisterAsync("Bob", second: Medium);

        await CloseAsync();

        var password = await AwardsByReasonAsync(DonkeyReasons.RegistrationPassword);
        password.Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 10, ["Bob"] = 5 });
    }

    [Fact]
    public async Task Registration_event_tab_shows_both_parts()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(5));
        await _factory.RegisterAsync("Bob", second: Strong);
        _factory.Time.Advance(TimeSpan.FromSeconds(5));
        await _factory.RegisterAsync("Ann", second: Medium);

        await CloseAsync();

        var admin = await TestClients.AdminAsync(_factory);
        var events = await admin.GetFromJsonAsync<List<EventDto>>("/api/events", TestClients.Json);
        var board = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={events!.Single().Id}", TestClients.Json);
        board!.Select(r => (r.Login, r.Donkeys)).Should().Equal(("Bob", 20), ("Ann", 10));
    }

    [Fact]
    public async Task Unfinished_attempts_get_nothing()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("Ann");
        await StartAsync(TestClients.Anonymous(_factory), "halfway", Weak);

        await CloseAsync();

        var admin = await TestClients.AdminAsync(_factory);
        var board = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json);
        board!.Select(r => r.Login).Should().Equal("Ann");
    }

    [Fact]
    public async Task Late_guest_after_close_gets_only_start_capital()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("Ann");
        await CloseAsync();

        var late = await _factory.RegisterAsync("late");

        var board = await late.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json);
        board!.Single(r => r.Login == "late").Donkeys.Should().Be(150);
        (await AwardsByReasonAsync(DonkeyReasons.RegistrationSpeed)).Should().NotContainKey("late");
    }

    [Fact]
    public async Task Close_when_not_open_returns_409()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Close_twice_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        await CloseAsync();
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Podium_scheme_applied()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await _factory.RegisterAsync("Ann");
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await _factory.RegisterAsync("Bob");
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await _factory.RegisterAsync("Cid");

        await CloseAsync("Podium");

        var speed = await AwardsByReasonAsync(DonkeyReasons.RegistrationSpeed);
        speed.Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 10, ["Bob"] = 7, ["Cid"] = 5 });
    }

    [Fact]
    public async Task Custom_scheme_applied()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await _factory.RegisterAsync("Ann");
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        await _factory.RegisterAsync("Bob");

        await CloseAsync("Custom", new Dictionary<int, int> { [1] = 8, [2] = 3 });

        var speed = await AwardsByReasonAsync(DonkeyReasons.RegistrationSpeed);
        speed.Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 8, ["Bob"] = 3 });
    }

    [Fact]
    public async Task Status_finished_after_close()
    {
        await _factory.OpenRegistrationAsync();

        await CloseAsync();

        var status = await TestClients.Anonymous(_factory).GetFromJsonAsync<System.Text.Json.JsonElement>("/api/registration/status");
        status.GetProperty("state").GetString().Should().Be("Finished");
    }

    public void Dispose() => _factory.Dispose();

    private async Task CloseAsync(string scheme = "Linear", Dictionary<int, int>? customTable = null)
    {
        var admin = await TestClients.AdminAsync(_factory);
        var response = await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme, customTable });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<Dictionary<string, int>> AwardsByReasonAsync(string reason)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShrekParty.Api.Data.AppDbContext>();
        return db.DonkeyAwards
            .Where(a => a.Reason == reason)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { u.Login, a.Amount })
            .ToDictionary(x => x.Login, x => x.Amount);
    }
}
