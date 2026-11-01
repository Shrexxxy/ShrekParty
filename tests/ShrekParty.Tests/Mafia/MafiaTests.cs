using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Mafia;

public class MafiaTests : IDisposable
{
    private static readonly string[] Six = ["Ann", "Bob", "Cid", "Dan", "Eve", "Fox"];

    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_needs_five()
    {
        await SeedAsync("Ann", "Bob", "Cid", "Dan");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Roles_dealt()
    {
        await SeedAsync(Six);
        var (_, admin) = await StartAsync();

        var roles = (await RolesAsync(admin)).Values.Select(r => r.Role).ToList();

        roles.Count(r => r == "Mafia").Should().Be(2);
        roles.Count(r => r == "Doctor").Should().Be(1);
        roles.Count(r => r == "Commissar").Should().Be(1);
        roles.Count(r => r == "Civilian").Should().Be(2);
    }

    [Fact]
    public async Task Player_sees_only_own_role_mafia_sees_partner()
    {
        await SeedAsync(Six);
        var (_, admin) = await StartAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();
        var civilian = roles.First(r => r.Value.Role == "Civilian").Key;

        var forCivilian = await DetailsAsync(await PlayerAsync(civilian));
        forCivilian.GetProperty("myRole").GetString().Should().Be("Civilian");
        VisibleRoles(forCivilian).Should().ContainSingle().Which.Should().Be(civilian);

        var forMafia = await DetailsAsync(await PlayerAsync(mafia[0]));
        forMafia.GetProperty("partners").EnumerateArray().Select(p => p.GetGuid()).Should().Equal(roles[mafia[1]].Id);
        VisibleRoles(forMafia).Should().BeEquivalentTo(mafia);
    }

    [Fact]
    public async Task Tv_sees_no_roles()
    {
        await SeedAsync(Six);
        await StartAsync();

        VisibleRoles(await DetailsAsync(await TestClients.ViewerAsync(_factory))).Should().BeEmpty();
    }

    [Fact]
    public async Task Intro_then_morning_without_kill_then_speech()
    {
        await SeedAsync(Six);
        var (_, admin) = await StartAsync();
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Intro");

        await AdvanceAsync(TimeSpan.FromSeconds(60));
        var morning = await DetailsAsync(admin);
        morning.GetProperty("phase").GetString().Should().Be("Morning");
        morning.GetProperty("nightVictim").ValueKind.Should().Be(JsonValueKind.Null);
        Alive(morning).Should().HaveCount(6);

        await AdvanceAsync(TimeSpan.FromSeconds(10));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Speech");
    }

    [Fact]
    public async Task Night_kill_when_mafia_agree()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var victim = roles.First(r => r.Value.Role == "Civilian").Key;

        foreach (var mafia in roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key))
        {
            await NightOkAsync(mafia, id, roles[victim].Id);
        }

        await AdvanceAsync(TimeSpan.FromSeconds(60));

        var morning = await DetailsAsync(admin);
        morning.GetProperty("phase").GetString().Should().Be("Morning");
        morning.GetProperty("nightVictim").GetGuid().Should().Be(roles[victim].Id);
        Alive(morning).Should().NotContain(victim);
    }

    [Fact]
    public async Task Mafia_disagree_nobody_dies()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var civilians = roles.Where(r => r.Value.Role == "Civilian").Select(r => roles[r.Key].Id).ToList();
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();

        await NightOkAsync(mafia[0], id, civilians[0]);
        await NightOkAsync(mafia[1], id, civilians[1]);
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        Alive(await DetailsAsync(admin)).Should().HaveCount(6);
    }

    [Fact]
    public async Task Doctor_saves()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var victim = roles.First(r => r.Value.Role == "Civilian").Key;
        foreach (var mafia in roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key))
        {
            await NightOkAsync(mafia, id, roles[victim].Id);
        }

        await NightOkAsync(Role(roles, "Doctor"), id, roles[victim].Id);
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        var morning = await DetailsAsync(admin);
        morning.GetProperty("nightVictim").ValueKind.Should().Be(JsonValueKind.Null);
        Alive(morning).Should().Contain(victim);
    }

    [Fact]
    public async Task Doctor_cannot_heal_self()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var doctor = Role(roles, "Doctor");

        (await NightAsync(doctor, id, roles[doctor].Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Doctor_not_same_twice_in_a_row()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var doctor = Role(roles, "Doctor");
        var patient = roles[Role(roles, "Commissar")].Id;
        await NightOkAsync(doctor, id, patient);
        await NextNightAsync(admin, id);

        (await NightAsync(doctor, id, patient)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Commissar_check_once_and_sees_result()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var commissar = Role(roles, "Commissar");
        var mafia = roles[Role(roles, "Mafia")].Id;

        await NightOkAsync(commissar, id, mafia);

        var check = (await DetailsAsync(await PlayerAsync(commissar))).GetProperty("checks").EnumerateArray().Single();
        check.GetProperty("targetId").GetGuid().Should().Be(mafia);
        check.GetProperty("isMafia").GetBoolean().Should().BeTrue();
        (await NightAsync(commissar, id, roles[Role(roles, "Doctor")].Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Civilian_pick_has_no_effect()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var civilians = roles.Where(r => r.Value.Role == "Civilian").Select(r => r.Key).ToList();

        await NightOkAsync(civilians[0], id, roles[civilians[1]].Id);
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        Alive(await DetailsAsync(admin)).Should().HaveCount(6);
    }

    [Fact]
    public async Task Night_ends_early_when_all_acted()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var victim = roles[roles.Keys.First(k => roles[k].Role == "Civilian")].Id;
        foreach (var (login, info) in roles)
        {
            var target = info.Role switch
            {
                "Mafia" => victim,
                "Doctor" or "Commissar" => roles[roles.Keys.First(k => roles[k].Role == "Mafia")].Id,
                _ => roles[roles.Keys.First(k => k != login)].Id,
            };
            await NightOkAsync(login, id, target);
        }

        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Morning");
    }

    [Fact]
    public async Task Dead_cannot_act()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var victim = Role(roles, "Civilian");
        foreach (var mafia in roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key))
        {
            await NightOkAsync(mafia, id, roles[victim].Id);
        }

        await NextNightAsync(admin, id);

        (await NightAsync(victim, id, roles[Role(roles, "Mafia")].Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Speaker_done_and_admin_next_pass_word()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToSpeechAsync();
        var first = await SpeakerAsync(admin);

        (await (await PlayerAsync(first)).PostAsync($"/api/mafia/{id}/speech-done", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var second = await SpeakerAsync(admin);
        second.Should().NotBe(first);
        (await DetailsAsync(admin)).GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(60));

        (await (await PlayerAsync(first)).PostAsync($"/api/mafia/{id}/speech-done", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await NextAsync(admin, id);
        (await SpeakerAsync(admin)).Should().NotBe(second);
    }

    [Fact]
    public async Task Speech_order_rotates_each_day()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToSpeechAsync();
        var dayOne = (await DetailsAsync(admin)).GetProperty("speechQueue").EnumerateArray().Select(q => q.GetGuid()).ToList();
        await NextNightAsync(admin, id, fromSpeech: true);
        await NextAsync(admin, id);
        await NextAsync(admin, id);

        var dayTwo = (await DetailsAsync(admin)).GetProperty("speechQueue").EnumerateArray().Select(q => q.GetGuid()).ToList();

        dayTwo[0].Should().Be(dayOne[1]);
    }

    [Fact]
    public async Task First_speaker_moves_to_next_living_seat()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var dayOne = (await DetailsAsync(admin)).GetProperty("speechQueue").EnumerateArray().Select(q => q.GetGuid()).ToList();
        var roles = await RolesAsync(admin);
        var opener = roles.Single(r => r.Value.Id == dayOne[0]).Key;

        // Открывавший первый день выбывает — второй день открывает следующий за ним живой.
        await EliminateAsync(admin, id, opener);
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        await NextAsync(admin, id);

        (await DetailsAsync(admin)).GetProperty("speechQueue")[0].GetGuid().Should().Be(dayOne[1]);
    }

    [Fact]
    public async Task Admin_next_with_stale_step_conflicts()
    {
        await SeedAsync(Six);
        var (id, admin) = await StartAsync();
        var step = (await DetailsAsync(admin)).GetProperty("step").GetInt32();

        // Таймер успел раньше нажатия ведущего.
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        (await admin.PostAsync($"/api/admin/mafia/{id}/next?step={step}", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var morning = await DetailsAsync(admin);
        morning.GetProperty("phase").GetString().Should().Be("Morning");
        (await admin.PostAsync($"/api/admin/mafia/{id}/next?step={morning.GetProperty("step").GetInt32()}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Speech");
    }

    [Fact]
    public async Task Ended_after_vote_shows_eliminated_not_old_victim()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var civilians = roles.Where(r => r.Value.Role == "Civilian").Select(r => r.Key).ToList();
        foreach (var mafia in roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key))
        {
            await NightOkAsync(mafia, id, roles[civilians[0]].Id);
        }

        // Ночь убила одного мирного, днём выгнали второго: 2 мафии на 2 — мафия побеждает на голосовании.
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        await SkipSpeechesAsync(admin, id);
        await EliminateAsync(admin, id, civilians[1]);

        var details = await DetailsAsync(await TestClients.ViewerAsync(_factory));
        details.GetProperty("phase").GetString().Should().Be("Ended");
        details.GetProperty("winner").GetString().Should().Be("Mafia");
        details.GetProperty("eliminated").GetProperty("userId").GetGuid().Should().Be(roles[civilians[1]].Id);
        details.GetProperty("nightVictim").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Vote_hidden_until_result_and_once()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var roles = await RolesAsync(admin);

        await VoteOkAsync("Ann", id, roles["Bob"].Id);

        (await DetailsAsync(await PlayerAsync("Cid"))).GetProperty("votes").GetArrayLength().Should().Be(0);
        (await DetailsAsync(await PlayerAsync("Cid"))).GetProperty("votedCount").GetInt32().Should().Be(1);
        (await VoteAsync("Ann", id, roles["Cid"].Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await VoteAsync("Bob", id, roles["Bob"].Id)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Majority_eliminates_reveals_role_then_last_word_then_night()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var roles = await RolesAsync(admin);
        var target = roles[Role(roles, "Doctor")];
        foreach (var login in Six)
        {
            await VoteOkAsync(login, id, roles[login].Id == target.Id ? null : target.Id);
        }

        var result = await DetailsAsync(await TestClients.ViewerAsync(_factory));
        result.GetProperty("phase").GetString().Should().Be("VoteResult");
        result.GetProperty("votes").GetArrayLength().Should().Be(6);
        result.GetProperty("eliminated").GetProperty("userId").GetGuid().Should().Be(target.Id);
        result.GetProperty("eliminated").GetProperty("role").GetString().Should().Be("Doctor");

        await AdvanceAsync(TimeSpan.FromSeconds(10));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("LastWord");
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Night");
    }

    [Fact]
    public async Task Tie_nobody_eliminated()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var roles = await RolesAsync(admin);
        await VoteOkAsync("Ann", id, roles["Bob"].Id);
        await VoteOkAsync("Bob", id, roles["Ann"].Id);
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        var result = await DetailsAsync(admin);
        result.GetProperty("phase").GetString().Should().Be("VoteResult");
        result.GetProperty("eliminated").ValueKind.Should().Be(JsonValueKind.Null);
        await AdvanceAsync(TimeSpan.FromSeconds(10));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Night");
    }

    [Fact]
    public async Task Civilians_win_and_rank()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();

        // Голосование → последнее слово → ночь (никого не трогаем) → утро → речи → второе голосование.
        await EliminateAsync(admin, id, mafia[0]);
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        await SkipSpeechesAsync(admin, id);
        await EliminateAsync(admin, id, mafia[1]);

        var details = await DetailsAsync(await TestClients.ViewerAsync(_factory));
        details.GetProperty("phase").GetString().Should().Be("Ended");
        details.GetProperty("winner").GetString().Should().Be("Civilians");
        VisibleRoles(details).Should().HaveCount(6);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();
        var tab = await TabAsync(admin, id);
        mafia.Should().OnlyContain(m => tab[m] == 25);
        Six.Except(mafia).Should().OnlyContain(c => tab[c] == 50);
    }

    [Fact]
    public async Task Mafia_win_when_equal_to_others()
    {
        await SeedAsync("Ann", "Bob", "Cid", "Dan", "Eve");
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();
        var others = roles.Keys.Except(mafia).ToList();

        // 5 игроков: 2 мафии и 3 остальных. Одна ночная жертва — 2 на 2, мафия побеждает.
        foreach (var m in mafia)
        {
            await NightOkAsync(m, id, roles[others[0]].Id);
        }

        await NextAsync(admin, id);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Ended");
        details.GetProperty("winner").GetString().Should().Be("Mafia");
    }

    [Fact]
    public async Task Player_cannot_press_next()
    {
        await SeedAsync(Six);
        var (id, _) = await StartAsync();

        (await (await PlayerAsync("Ann")).PostAsync($"/api/admin/mafia/{id}/next", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedAsync(params string[] logins)
    {
        foreach (var login in logins)
        {
            await _factory.SeedPlayerAsync(login);
        }
    }

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Mafia", title = "Мафия" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> ToSpeechAsync()
    {
        var (id, admin) = await StartAsync();
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> ToVotingAsync()
    {
        var (id, admin) = await ToSpeechAsync();
        await SkipSpeechesAsync(admin, id);
        return (id, admin);
    }

    /// <summary>Знакомство → утро → речи → голосование без голосов → итог → ночь.</summary>
    private async Task<(Guid Id, HttpClient Admin)> ToNightAsync()
    {
        var (id, admin) = await ToVotingAsync();
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Night");
        return (id, admin);
    }

    /// <summary>Из ночи (или из речей) — до следующей ночи, без голосов.</summary>
    private async Task NextNightAsync(HttpClient admin, Guid id, bool fromSpeech = false)
    {
        if (!fromSpeech)
        {
            await NextAsync(admin, id);
            await NextAsync(admin, id);
        }

        await SkipSpeechesAsync(admin, id);
        await NextAsync(admin, id);
        await NextAsync(admin, id);
    }

    private async Task SkipSpeechesAsync(HttpClient admin, Guid id)
    {
        while ((await DetailsAsync(admin)).GetProperty("phase").GetString() == "Speech")
        {
            await NextAsync(admin, id);
        }
    }

    /// <summary>Все живые голосуют против игрока (он сам воздерживается), итог.</summary>
    private async Task EliminateAsync(HttpClient admin, Guid id, string login)
    {
        var details = await DetailsAsync(admin);
        var target = (await RolesAsync(admin))[login].Id;
        foreach (var voter in Alive(details))
        {
            var response = await VoteAsync(voter, id, voter == login ? null : target);
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                break;
            }
        }

        await AdvanceAsync(TimeSpan.FromSeconds(60));
        await NextAsync(admin, id);
    }

    private static async Task NextAsync(HttpClient admin, Guid id) =>
        (await admin.PostAsync($"/api/admin/mafia/{id}/next", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private async Task<HttpResponseMessage> NightAsync(string login, Guid id, Guid targetId) =>
        await (await PlayerAsync(login)).PostAsJsonAsync($"/api/mafia/{id}/night", new { targetId });

    private async Task NightOkAsync(string login, Guid id, Guid targetId) =>
        (await NightAsync(login, id, targetId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task<HttpResponseMessage> VoteAsync(string login, Guid id, Guid? targetId) =>
        await (await PlayerAsync(login)).PostAsJsonAsync($"/api/mafia/{id}/vote", new { targetId });

    private async Task VoteOkAsync(string login, Guid id, Guid? targetId) =>
        (await VoteAsync(login, id, targetId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static async Task<Dictionary<string, (Guid Id, string Role)>> RolesAsync(HttpClient admin) =>
        (await DetailsAsync(admin)).GetProperty("players").EnumerateArray()
            .ToDictionary(p => p.GetProperty("login").GetString()!, p => (p.GetProperty("userId").GetGuid(), p.GetProperty("role").GetString()!));

    private static string Role(Dictionary<string, (Guid Id, string Role)> roles, string role) => roles.First(r => r.Value.Role == role).Key;

    private static List<string> VisibleRoles(JsonElement details) =>
        details.GetProperty("players").EnumerateArray()
            .Where(p => p.GetProperty("role").ValueKind == JsonValueKind.String)
            .Select(p => p.GetProperty("login").GetString()!)
            .ToList();

    private static List<string> Alive(JsonElement details) =>
        details.GetProperty("players").EnumerateArray()
            .Where(p => p.GetProperty("alive").GetBoolean())
            .Select(p => p.GetProperty("login").GetString()!)
            .ToList();

    private async Task<string> SpeakerAsync(HttpClient admin)
    {
        var details = await DetailsAsync(admin);
        var speaker = details.GetProperty("speakerId").GetGuid();
        return details.GetProperty("players").EnumerateArray().Single(p => p.GetProperty("userId").GetGuid() == speaker).GetProperty("login").GetString()!;
    }

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid id)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }
}
