using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Registration;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Registration;

public class RegistrationResultsTests : IDisposable
{
    private const string Medium = "Correct-Horse-7";

    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Results_empty_before_close()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("bob");

        (await ResultsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Results_have_columns()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(10));
        await _factory.RegisterAsync("bob", second: RegistrationSteps.Strong);
        _factory.Time.Advance(TimeSpan.FromSeconds(65));
        await _factory.RegisterAsync("ann", second: Medium);
        await CloseAsync();

        var rows = await ResultsAsync();

        rows.Select(r => r.Login).Should().Equal("bob", "ann");
        var bob = rows[0];
        bob.Place.Should().Be(1);
        bob.SpeedPlace.Should().Be(1);
        bob.ElapsedSeconds.Should().Be(10);
        bob.SpeedDonkeys.Should().Be(10);
        bob.PasswordDonkeys.Should().Be(10);
        bob.TotalDonkeys.Should().Be(20);
        bob.PasswordScore.Should().Be(4);
        var ann = rows[1];
        ann.Place.Should().Be(2);
        ann.SpeedPlace.Should().Be(2);
        ann.ElapsedSeconds.Should().Be(75);
        ann.SpeedDonkeys.Should().Be(5);
        ann.PasswordDonkeys.Should().Be(5);
        ann.TotalDonkeys.Should().Be(10);
        ann.PasswordScore.Should().BeGreaterThanOrEqualTo(3);
        ann.PasswordGuessesLog10.Should().BeLessThan(bob.PasswordGuessesLog10);
    }

    [Fact]
    public async Task Late_guests_listed_last_without_places()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(10));
        await _factory.RegisterAsync("bob");
        await CloseAsync();
        _factory.Time.Advance(TimeSpan.FromMinutes(1));

        await _factory.RegisterAsync("late", second: RegistrationSteps.Strong);

        var rows = await ResultsAsync();
        rows.Select(r => r.Login).Should().Equal("bob", "late");
        rows[0].IsLate.Should().BeFalse();
        var late = rows[1];
        late.IsLate.Should().BeTrue();
        late.Place.Should().BeNull();
        late.SpeedPlace.Should().BeNull();
        late.ElapsedSeconds.Should().Be(70);
        late.PasswordScore.Should().Be(4);
        late.TotalDonkeys.Should().Be(0);
    }

    [Fact]
    public async Task Late_guest_does_not_change_places()
    {
        await _factory.OpenRegistrationAsync();
        await _factory.RegisterAsync("bob");
        await _factory.RegisterAsync("ann");
        await CloseAsync();
        _factory.Time.Advance(TimeSpan.FromMinutes(1));

        await _factory.RegisterAsync("late");

        var inTime = (await ResultsAsync()).Where(r => !r.IsLate).ToList();
        inTime.Select(r => r.SpeedPlace).Should().Equal(1, 1);
    }

    [Fact]
    public async Task Status_has_closed_at_after_close()
    {
        await _factory.OpenRegistrationAsync();
        _factory.Time.Advance(TimeSpan.FromMinutes(3));

        await CloseAsync();

        var status = await TestClients.Anonymous(_factory).GetFromJsonAsync<RegistrationStatusDto>("/api/registration/status", TestClients.Json);
        status!.ClosedAt.Should().Be(_factory.Time.GetUtcNow());
    }

    [Fact]
    public async Task Results_require_auth()
    {
        var response = await TestClients.Anonymous(_factory).GetAsync("/api/registration/results");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
    }

    public void Dispose() => _factory.Dispose();

    private async Task<List<RegistrationResultRowDto>> ResultsAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        return (await admin.GetFromJsonAsync<List<RegistrationResultRowDto>>("/api/registration/results", TestClients.Json))!;
    }

    private async Task CloseAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { scheme = "Linear" })).EnsureSuccessStatusCode();
    }
}
