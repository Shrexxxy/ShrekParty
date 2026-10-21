using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Admin;

public class ResetTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Reset_removes_players_keeps_staff()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya");
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" })).EnsureSuccessStatusCode();

        var reset = await admin.PostAsync("/api/admin/reset", null);

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json)).Should().BeEmpty();
        var status = await admin.GetFromJsonAsync<JsonElement>("/api/registration/status");
        status.GetProperty("state").GetString().Should().Be("Closed");
        (await TestClients.AdminAsync(_factory)).Should().NotBeNull();
        (await TestClients.ViewerAsync(_factory)).Should().NotBeNull();
    }

    [Fact]
    public async Task Reset_allows_same_login_again()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("petya");
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        await _factory.OpenRegistrationAsync();
        var again = await _factory.RegisterAsync("petya");

        (await again.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_deletes_avatar_files()
    {
        await _factory.OpenRegistrationAsync();
        var player = await _factory.RegisterAsync("petya");
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" })).EnsureSuccessStatusCode();
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        (await player.PostAsync("/api/me/avatar", new MultipartFormDataContent { { file, "file", "selfie" } })).EnsureSuccessStatusCode();
        var mediaDir = Path.Combine(_factory.DataDir, "media");
        Directory.GetFiles(mediaDir).Should().NotBeEmpty();

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        Directory.GetFiles(mediaDir).Should().BeEmpty();
    }

    [Fact]
    public async Task Player_cannot_reset()
    {
        await _factory.SeedPlayerAsync("petya");
        var player = await TestClients.LoggedInAsync(_factory, "petya", TestData.PlayerPassword);

        (await player.PostAsync("/api/admin/reset", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose() => _factory.Dispose();
}
