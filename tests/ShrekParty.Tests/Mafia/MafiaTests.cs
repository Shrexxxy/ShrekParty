using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
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

    #region Задания и ослики

    [Fact]
    public async Task Civilian_does_five_tasks_and_gets_one_each()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var civilian = Role(roles, "Civilian");

        for (var i = 0; i < 5; i++)
        {
            await TaskOkAsync(civilian, id);
        }

        (await TaskAsync(civilian, id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailsAsync(await PlayerAsync(civilian))).GetProperty("myTasksDone").GetInt32().Should().Be(5);
        await NextAsync(admin, id);
        Bonus(await DetailsAsync(await PlayerAsync(civilian)), roles[civilian].Id, "Tasks").Should().Be((5, 5));
    }

    [Fact]
    public async Task Task_too_fast_rejected()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var civilian = Role(await RolesAsync(admin), "Civilian");

        (await TaskAsync(civilian, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await TaskAsync(civilian, id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Role_tasks_count_but_give_nothing()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var doctor = Role(await RolesAsync(admin), "Doctor");

        await TaskOkAsync(doctor, id);
        await TaskOkAsync(doctor, id);

        (await DetailsAsync(await PlayerAsync(doctor))).GetProperty("myTasksDone").GetInt32().Should().Be(2);
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("bonuses").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task No_tasks_outside_night_or_when_dead()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToSpeechAsync();
        (await TaskAsync("Ann", id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        await NextNightAsync(admin, id, fromSpeech: true);
        var roles = await RolesAsync(admin);
        var victim = Role(roles, "Civilian");
        await KillAsync(roles, id, victim);
        await NextNightAsync(admin, id);

        (await TaskAsync(victim, id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Civilian_has_no_night_action()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var civilians = roles.Where(r => r.Value.Role == "Civilian").Select(r => r.Key).ToList();

        (await NightAsync(civilians[0], id, roles[civilians[1]].Id)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Night_ends_early_when_roles_acted_and_civilians_did_five()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();
        var civilians = roles.Where(r => r.Value.Role == "Civilian").Select(r => r.Key).ToList();
        await KillAsync(roles, id, civilians[0]);
        await NightOkAsync(Role(roles, "Doctor"), id, roles[mafia[0]].Id);
        await NightOkAsync(Role(roles, "Commissar"), id, roles[mafia[0]].Id);
        foreach (var civilian in civilians)
        {
            for (var i = 0; i < 5; i++)
            {
                (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Night");
                await TaskOkAsync(civilian, id);
            }
        }

        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Morning");
    }

    [Fact]
    public async Task Kill_gives_ten_to_each_alive_mafia()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();
        var civilians = roles.Where(r => r.Value.Role == "Civilian").Select(r => r.Key).ToList();
        await KillAsync(roles, id, civilians[0]);
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        await SkipSpeechesAsync(admin, id);

        // Первую мафию выгнали — за второе убийство она уже ничего не получает.
        await EliminateAsync(admin, id, mafia[0]);
        await KillAsync(roles, id, civilians[1]);
        await NextAsync(admin, id);

        var details = await DetailsAsync(admin);
        Bonus(details, roles[mafia[0]].Id, "Kill").Should().Be((1, 10));
        Bonus(details, roles[mafia[1]].Id, "Kill").Should().Be((2, 20));
    }

    [Fact]
    public async Task Doctor_save_gives_fifteen_and_mafia_nothing()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var victim = Role(roles, "Civilian");
        await KillAsync(roles, id, victim);
        await NightOkAsync(Role(roles, "Doctor"), id, roles[victim].Id);

        await NextAsync(admin, id);

        var details = await DetailsAsync(admin);
        Bonus(details, roles[Role(roles, "Doctor")].Id, "Save").Should().Be((1, 15));
        details.GetProperty("bonuses").EnumerateArray().Should().NotContain(b => b.GetProperty("kind").GetString() == "Kill");
    }

    [Fact]
    public async Task Commissar_gets_ten_for_each_mafia_found_once()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var commissar = Role(roles, "Commissar");
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => roles[r.Key].Id).ToList();

        await NightOkAsync(commissar, id, mafia[0]);
        await NextNightAsync(admin, id);
        await NightOkAsync(commissar, id, mafia[0]);
        await NextNightAsync(admin, id);
        await NightOkAsync(commissar, id, roles[Role(roles, "Doctor")].Id);
        await NextNightAsync(admin, id);
        await NightOkAsync(commissar, id, mafia[1]);

        Bonus(await DetailsAsync(admin), roles[commissar].Id, "Check").Should().Be((2, 20));
    }

    [Fact]
    public async Task Oust_gives_ten_to_alive_non_mafia()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();

        await EliminateAsync(admin, id, mafia[0]);

        var details = await DetailsAsync(admin);
        foreach (var login in Six.Except(mafia))
        {
            Bonus(details, roles[login].Id, "Oust").Should().Be((1, 10));
        }

        Owners(details).Should().NotContain(mafia.Select(m => roles[m].Id));
    }

    [Fact]
    public async Task Finish_after_win_writes_win_and_bonus_rows()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();
        var dead = Role(roles, "Civilian");

        // Ночью убит мирный, днём выгнали первую мафию, следующей ночью никого, вторым днём — вторую.
        await KillAsync(roles, id, dead);
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        await SkipSpeechesAsync(admin, id);
        await EliminateAsync(admin, id, mafia[0]);
        await NextAsync(admin, id);
        await NextAsync(admin, id);
        await SkipSpeechesAsync(admin, id);
        await EliminateAsync(admin, id, mafia[1]);
        var ended = await DetailsAsync(await TestClients.ViewerAsync(_factory));
        ended.GetProperty("winner").GetString().Should().Be("Civilians");
        ended.GetProperty("winDonkeys").GetInt32().Should().Be(25);
        Owners(ended).Should().HaveCount(5);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        var rows = await RowsAsync(id);
        foreach (var login in Six.Except(mafia))
        {
            rows.Should().Contain((login, "Мафия: победа", 25));
        }

        rows.Should().Contain((Role(roles, "Doctor"), "Мафия: посадки", 20));
        rows.Should().Contain((mafia[0], "Мафия: убийства", 10));
        rows.Should().NotContain(r => r.Login == dead && r.Reason == "Мафия: посадки");
        rows.Should().NotContain(r => mafia.Contains(r.Login) && r.Reason == "Мафия: победа");
    }

    [Fact]
    public async Task Finish_without_winner_writes_only_bonuses()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var mafia = roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key).ToList();
        await KillAsync(roles, id, Role(roles, "Civilian"));
        await NextAsync(admin, id);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        (await RowsAsync(id)).Should().BeEquivalentTo(mafia.Select(m => (m, "Мафия: убийства", 10)));
    }

    [Fact]
    public async Task Bonuses_visible_only_to_owner_admin_and_in_the_end()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToNightAsync();
        var roles = await RolesAsync(admin);
        var mafia = Role(roles, "Mafia");
        var civilian = Role(roles, "Civilian");
        await TaskOkAsync(civilian, id);
        await TaskOkAsync(mafia, id);
        var tv = await TestClients.ViewerAsync(_factory);
        (await DetailsAsync(tv)).GetProperty("tasksTotal").GetInt32().Should().Be(2);
        (await DetailsAsync(tv)).GetProperty("tasks").GetArrayLength().Should().Be(0);
        (await DetailsAsync(admin)).GetProperty("tasks").GetArrayLength().Should().Be(2);
        await KillAsync(roles, id, Role(roles, "Doctor"));

        await NextAsync(admin, id);

        // Убийство — обеим мафиям, задание — мирному: у ведущего трое, у каждого — только своё.
        Owners(await DetailsAsync(await PlayerAsync(mafia))).Should().Equal(roles[mafia].Id);
        Owners(await DetailsAsync(await PlayerAsync(civilian))).Should().Equal(roles[civilian].Id);
        Owners(await DetailsAsync(tv)).Should().BeEmpty();
        Owners(await DetailsAsync(admin)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Finish_counts_bonus_saved_after_engine_read_event()
    {
        await SeedAsync(Six);
        var (id, admin) = await ToVotingAsync();
        var roles = await RolesAsync(admin);
        var mafia = Role(roles, "Mafia");
        var voters = Six.Where(l => l != mafia).ToList();
        foreach (var voter in voters.Skip(1))
        {
            await VoteOkAsync(voter, id, roles[mafia].Id);
        }

        // Движок уже прочитал ивент, когда пришёл последний голос: посадка мафии должна попасть в начисление.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Events.FindAsync(id);
        await VoteOkAsync(voters[0], id, roles[mafia].Id);
        await VoteOkAsync(mafia, id, null);
        await scope.ServiceProvider.GetRequiredService<EventEngine>().FinishAsync(id);

        (await RowsAsync(id)).Should().Contain((Role(roles, "Doctor"), "Мафия: посадки", 10));
    }

    #endregion

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

    private async Task<HttpResponseMessage> TaskAsync(string login, Guid id) =>
        await (await PlayerAsync(login)).PostAsync($"/api/mafia/{id}/task-done", null);

    /// <summary>Задание сделано, и прошло 2 с — следующее уже не «слишком быстро».</summary>
    private async Task TaskOkAsync(string login, Guid id)
    {
        (await TaskAsync(login, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AdvanceAsync(TimeSpan.FromSeconds(2));
    }

    /// <summary>Все живые мафии выбирают жертву (выбывшая получит 409 — её пропускаем).</summary>
    private async Task KillAsync(Dictionary<string, (Guid Id, string Role)> roles, Guid id, string victim)
    {
        foreach (var mafia in roles.Where(r => r.Value.Role == "Mafia").Select(r => r.Key))
        {
            await NightAsync(mafia, id, roles[victim].Id);
        }
    }

    private static (int Count, int Amount) Bonus(JsonElement details, Guid userId, string kind) =>
        details.GetProperty("bonuses").EnumerateArray()
            .Where(b => b.GetProperty("userId").GetGuid() == userId && b.GetProperty("kind").GetString() == kind)
            .Select(b => (b.GetProperty("count").GetInt32(), b.GetProperty("amount").GetInt32()))
            .SingleOrDefault();

    private static List<Guid> Owners(JsonElement details) =>
        details.GetProperty("bonuses").EnumerateArray().Select(b => b.GetProperty("userId").GetGuid()).Distinct().ToList();

    private async Task<List<(string Login, string Reason, int Amount)>> RowsAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.DonkeyAwards.Where(a => a.EventId == id)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { u.Login, a.Reason, a.Amount })
            .ToListAsync();
        return rows.Select(r => (r.Login, r.Reason, r.Amount)).ToList();
    }

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
