using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Tests.Infrastructure;
using static ShrekParty.Tests.Infrastructure.RegistrationSteps;

namespace ShrekParty.Tests.Registration;

public class RegistrationFlowTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_when_closed_returns_403()
    {
        var response = await StartAsync(TestClients.Anonymous(_factory), "petya", Weak);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Full_flow_creates_player_with_start_capital()
    {
        await _factory.OpenRegistrationAsync();
        var client = TestClients.Anonymous(_factory);

        var start = await StartAsync(client, "petya", Weak);
        var attempt = await start.Content.ReadFromJsonAsync<StartResponse>(TestClients.Json);
        var complete = await CompleteAsync(client, attempt!.AttemptId, Strong);

        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await complete.Content.ReadFromJsonAsync<MeDto>(TestClients.Json))!.Role.Should().Be(UserRole.Player);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        var board = await client.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json);
        board!.Single().Should().Match<LeaderboardRowDto>(r => r.Login == "petya" && r.Donkeys == 150);
    }

    [Fact]
    public async Task Complete_with_weak_password_creates_player()
    {
        await _factory.OpenRegistrationAsync();
        var client = TestClients.Anonymous(_factory);
        var attempt = await StartOkAsync(client, "petya", Weak);

        var complete = await CompleteAsync(client, attempt, "qwerty123");

        complete.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PlayersCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Complete_with_same_password_returns_same()
    {
        await _factory.OpenRegistrationAsync();
        var client = TestClients.Anonymous(_factory);
        var attempt = await StartOkAsync(client, "petya", Strong);

        var complete = await CompleteAsync(client, attempt, Strong);

        complete.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorAsync(complete)).Should().Be("same");
    }

    [Fact]
    public async Task Duplicate_login_case_insensitive_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        await StartOkAsync(TestClients.Anonymous(_factory), "Petya", Weak);

        var second = await StartAsync(TestClients.Anonymous(_factory), "petya", "другой");

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_of_registered_player_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya");

        var again = await StartAsync(TestClients.Anonymous(_factory), "PETYA", Weak);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Restart_with_same_credentials_resumes_attempt()
    {
        await _factory.OpenRegistrationAsync();

        var first = await StartOkAsync(TestClients.Anonymous(_factory), "petya", Weak);
        var second = await StartOkAsync(TestClients.Anonymous(_factory), "petya", Weak);

        second.Should().Be(first);
    }

    [Fact]
    public async Task Restart_with_other_password_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        await StartOkAsync(TestClients.Anonymous(_factory), "petya", Weak);

        var again = await StartAsync(TestClients.Anonymous(_factory), "petya", "54321");

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("слишком_длинный_логин_123")]
    [InlineData("пробел внутри")]
    [InlineData("")]
    public async Task Invalid_login_returns_400(string login)
    {
        await _factory.OpenRegistrationAsync();

        var response = await StartAsync(TestClients.Anonymous(_factory), login, Weak);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cyrillic_login_is_allowed()
    {
        await _factory.OpenRegistrationAsync();

        var response = await StartAsync(TestClients.Anonymous(_factory), "Витян_92", Weak);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Empty_password_returns_400()
    {
        await _factory.OpenRegistrationAsync();

        var response = await StartAsync(TestClients.Anonymous(_factory), "petya", "");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Weak_first_password_stores_mask_strong_does_not()
    {
        await _factory.OpenRegistrationAsync();
        await StartOkAsync(TestClients.Anonymous(_factory), "weakling", Weak);
        await StartOkAsync(TestClients.Anonymous(_factory), "strongman", Strong);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.RegistrationAttempts.SingleAsync(a => a.Login == "weakling")).FirstMask.Should().NotBeNull();
        (await db.RegistrationAttempts.SingleAsync(a => a.Login == "strongman")).FirstMask.Should().BeNull();
    }

    [Fact]
    public async Task Complete_unknown_attempt_returns_404()
    {
        await _factory.OpenRegistrationAsync();

        var response = await CompleteAsync(TestClients.Anonymous(_factory), Guid.NewGuid(), Strong);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_twice_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        var client = TestClients.Anonymous(_factory);
        var attempt = await StartOkAsync(client, "petya", Weak);
        (await CompleteAsync(client, attempt, Strong)).EnsureSuccessStatusCode();

        var again = await CompleteAsync(TestClients.Anonymous(_factory), attempt, Strong + "1");

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Status_reflects_open()
    {
        var client = TestClients.Anonymous(_factory);
        (await StatusAsync(client)).GetProperty("state").GetString().Should().Be("Closed");

        await _factory.OpenRegistrationAsync();

        (await StatusAsync(client)).GetProperty("state").GetString().Should().Be("Open");
    }

    [Fact]
    public async Task Open_twice_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        var admin = await TestClients.AdminAsync(_factory);

        var again = await admin.PostAsync("/api/admin/registration/open", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Player_cannot_open_registration()
    {
        await _factory.SeedPlayerAsync("petya");
        var player = await TestClients.LoggedInAsync(_factory, "petya", TestData.PlayerPassword);

        var response = await player.PostAsync("/api/admin/registration/open", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> StartOkAsync(HttpClient client, string login, string password)
    {
        var response = await StartAsync(client, login, password);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<StartResponse>(TestClients.Json))!.AttemptId;
    }

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();

    private static async Task<JsonElement> StatusAsync(HttpClient client) =>
        await client.GetFromJsonAsync<JsonElement>("/api/registration/status");

    private async Task<int> PlayersCountAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Users.CountAsync(u => u.Role == UserRole.Player);
    }
}
