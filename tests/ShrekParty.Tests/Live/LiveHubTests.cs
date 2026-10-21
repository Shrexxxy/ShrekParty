using AwesomeAssertions;
using ShrekParty.Api.Registration;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Live;

public class LiveHubTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Connect_receives_snapshot_immediately()
    {
        await using var guest = await LiveClient.ConnectAsync(_factory);

        var snapshot = await guest.WaitForAsync(_ => true);

        snapshot.Registration.State.Should().Be(RegistrationState.Closed);
    }

    [Fact]
    public async Task Waiting_count_tracks_anonymous_connections()
    {
        await using var admin = await LiveClient.ConnectAsync(_factory, await AdminCookieAsync());
        var guests = new List<LiveClient>();
        for (var i = 0; i < 3; i++)
        {
            guests.Add(await LiveClient.ConnectAsync(_factory));
        }

        await admin.WaitForAsync(s => s.Registration.WaitingCount == 3);
        await guests[0].DisposeAsync();

        await admin.WaitForAsync(s => s.Registration.WaitingCount == 2);
        foreach (var guest in guests.Skip(1))
        {
            await guest.DisposeAsync();
        }
    }

    [Fact]
    public async Task Open_registration_broadcasts_open()
    {
        await using var guest = await LiveClient.ConnectAsync(_factory);
        await guest.WaitForAsync(_ => true);

        await _factory.OpenRegistrationAsync();

        await guest.WaitForAsync(s => s.Registration.State == RegistrationState.Open);
    }

    [Fact]
    public async Task Completed_registration_updates_leaderboard_for_players()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.SeedPlayerAsync("watcher");
        await using var watcher = await LiveClient.ConnectAsync(
            _factory, await AuthCookies.LoginAsync(_factory, "watcher", TestData.PlayerPassword));

        await _factory.RegisterAsync("petya");

        await watcher.WaitForAsync(s => s.Leaderboard.Any(r => r.Login == "petya" && r.Donkeys == 150));
    }

    [Fact]
    public async Task Reconnect_gets_fresh_snapshot()
    {
        var first = await LiveClient.ConnectAsync(_factory);
        await first.WaitForAsync(_ => true);
        await first.DisposeAsync();

        await _factory.OpenRegistrationAsync();
        await using var again = await LiveClient.ConnectAsync(_factory);

        var snapshot = await again.WaitForAsync(_ => true);
        snapshot.Registration.State.Should().Be(RegistrationState.Open);
    }

    [Fact]
    public async Task Anonymous_snapshot_has_no_leaderboard()
    {
        await _factory.SeedPlayerAsync("petya");

        await using var guest = await LiveClient.ConnectAsync(_factory);

        (await guest.WaitForAsync(_ => true)).Leaderboard.Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_snapshot_has_leaderboard()
    {
        await _factory.SeedPlayerAsync("petya");

        await using var admin = await LiveClient.ConnectAsync(_factory, await AdminCookieAsync());

        (await admin.WaitForAsync(_ => true)).Leaderboard.Select(r => r.Login).Should().Equal("petya");
    }

    [Fact]
    public async Task Logged_in_connections_are_not_waiting()
    {
        await using var admin = await LiveClient.ConnectAsync(_factory, await AdminCookieAsync());

        (await admin.WaitForAsync(_ => true)).Registration.WaitingCount.Should().Be(0);
    }

    public void Dispose() => _factory.Dispose();

    private Task<string> AdminCookieAsync() =>
        AuthCookies.LoginAsync(_factory, AppFactory.AdminLogin, AppFactory.AdminPassword);
}
