using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using ShrekParty.Api.Auth;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Media;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Media;

public class AvatarTests : IDisposable
{
    /// <summary>PNG 1×1.</summary>
    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    /// <summary>Единственный игрок: 150 стартовых + 10 за скорость + 10 за пароль.</summary>
    private const int StartPlusRegistration = 170;

    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Avatar_before_close_returns_409()
    {
        await _factory.OpenRegistrationAsync();
        var player = await _factory.RegisterAsync("petya");

        var response = await UploadAsync(player, Png, "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task First_avatar_awards_5()
    {
        var player = await RegisteredAfterCloseAsync();

        var response = await UploadAsync(player, Png, "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await response.Content.ReadFromJsonAsync<MeDto>(TestClients.Json);
        me!.AvatarUrl.Should().NotBeNull();
        (await DonkeysAsync(player)).Should().Be(StartPlusRegistration + 5);
        var file = await player.GetAsync(me.AvatarUrl);
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(Png);
    }

    [Fact]
    public async Task Second_avatar_no_extra_points()
    {
        var player = await RegisteredAfterCloseAsync();
        var first = await (await UploadAsync(player, Png, "image/png")).Content.ReadFromJsonAsync<MeDto>(TestClients.Json);

        var second = await (await UploadAsync(player, Png, "image/png")).Content.ReadFromJsonAsync<MeDto>(TestClients.Json);

        second!.AvatarUrl.Should().NotBe(first!.AvatarUrl);
        (await DonkeysAsync(player)).Should().Be(StartPlusRegistration + 5);
    }

    [Fact]
    public async Task Avatar_shown_in_leaderboard()
    {
        var player = await RegisteredAfterCloseAsync();
        var me = await (await UploadAsync(player, Png, "image/png")).Content.ReadFromJsonAsync<MeDto>(TestClients.Json);

        var board = await player.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json);

        board!.Single().AvatarUrl.Should().Be(me!.AvatarUrl);
    }

    [Fact]
    public async Task Non_image_returns_400()
    {
        var player = await RegisteredAfterCloseAsync();

        var response = await UploadAsync(player, "привет"u8.ToArray(), "text/plain");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Too_large_returns_400()
    {
        var player = await RegisteredAfterCloseAsync();

        var response = await UploadAsync(player, new byte[AvatarEndpoints.MaxUploadBytes + 1], "image/jpeg");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_cannot_upload_avatar()
    {
        await _factory.OpenRegistrationAsync();
        await CloseAsync();

        var response = await UploadAsync(await TestClients.AdminAsync(_factory), Png, "image/png");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Media_requires_auth()
    {
        var player = await RegisteredAfterCloseAsync();
        var me = await (await UploadAsync(player, Png, "image/png")).Content.ReadFromJsonAsync<MeDto>(TestClients.Json);

        var response = await TestClients.Anonymous(_factory).GetAsync(me!.AvatarUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _factory.Dispose();

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", "selfie" } };
        return client.PostAsync("/api/me/avatar", form);
    }

    private async Task<HttpClient> RegisteredAfterCloseAsync()
    {
        await _factory.OpenRegistrationAsync();
        var player = await _factory.RegisterAsync("petya");
        await CloseAsync();
        return player;
    }

    private async Task CloseAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" })).EnsureSuccessStatusCode();
    }

    private static async Task<int> DonkeysAsync(HttpClient player)
    {
        var board = await player.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json);
        return board!.Single().Donkeys;
    }
}
