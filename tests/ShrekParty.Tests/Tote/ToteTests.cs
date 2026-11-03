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

namespace ShrekParty.Tests.Tote;

public class ToteTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Bet_deducts_balance_into_tote_tab()
    {
        var t = await SetupAsync(4);
        var (match, bettor) = await OpenMatchAndBettorAsync(t);

        await BetOkAsync(bettor.Client, match, PlayerId(match, "playerA"), 20);

        (await TotalAsync(t.Admin, bettor.Login)).Should().Be(130);
        (await ToteTabAsync(t)).Should().Contain(bettor.Login, -20);
    }

    [Theory]
    [InlineData(26, HttpStatusCode.Conflict, "limit")]
    [InlineData(0, HttpStatusCode.BadRequest, "bad_amount")]
    public async Task Bet_amount_limits(int amount, HttpStatusCode status, string error)
    {
        var t = await SetupAsync(4);
        var (match, bettor) = await OpenMatchAndBettorAsync(t);

        var response = await BetAsync(bettor.Client, match, PlayerId(match, "playerA"), amount);

        response.StatusCode.Should().Be(status);
        (await ErrorAsync(response)).Should().Be(error);
    }

    [Fact]
    public async Task Bet_over_balance_409()
    {
        var t = await SetupAsync(4, startCapital: 10);
        var (match, bettor) = await OpenMatchAndBettorAsync(t);

        var response = await BetAsync(bettor.Client, match, PlayerId(match, "playerA"), 20);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("balance");
    }

    [Fact]
    public async Task Bet_on_own_match_409()
    {
        var t = await SetupAsync(4);
        var match = Matches(await DetailsAsync(t.Admin), "Semi")[0];
        var player = t.Players.Single(p => p.Id == PlayerId(match, "playerA"));

        var response = await BetAsync(player.Client, match, PlayerId(match, "playerB"), 5);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("own_match");
    }

    [Fact]
    public async Task Bet_after_match_started_409()
    {
        var t = await SetupAsync(4);
        var (match, bettor) = await OpenMatchAndBettorAsync(t);
        (await t.Admin.PostAsync($"/api/admin/matches/{MatchId(match)}/start", null)).EnsureSuccessStatusCode();

        var response = await BetAsync(bettor.Client, match, PlayerId(match, "playerA"), 5);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("closed");
    }

    [Fact]
    public async Task Second_bet_on_same_match_409()
    {
        var t = await SetupAsync(4);
        var (match, bettor) = await OpenMatchAndBettorAsync(t);
        await BetOkAsync(bettor.Client, match, PlayerId(match, "playerA"), 5);

        var response = await BetAsync(bettor.Client, match, PlayerId(match, "playerB"), 5);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("already");
    }

    [Fact]
    public async Task Bet_on_player_not_in_match_400()
    {
        var t = await SetupAsync(4);
        var (match, bettor) = await OpenMatchAndBettorAsync(t);

        var response = await BetAsync(bettor.Client, match, bettor.Id, 5);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Winners_are_paid_losers_lose_stake()
    {
        var t = await SetupAsync(4);
        var match = Matches(await DetailsAsync(t.Admin), "Semi")[0];
        var (a, b) = (PlayerId(match, "playerA"), PlayerId(match, "playerB"));
        var others = t.Players.Where(p => p.Id != a && p.Id != b).ToList();
        await BetOkAsync(others[0].Client, match, a, 20);
        await BetOkAsync(others[1].Client, match, b, 10);

        await WinAsync(t.Admin, match, a);

        var tab = await ToteTabAsync(t);
        tab[others[0].Login].Should().Be(10);
        tab[others[1].Login].Should().Be(-10);
    }

    [Fact]
    public async Task Nobody_guessed_everyone_refunded()
    {
        var t = await SetupAsync(4);
        var match = Matches(await DetailsAsync(t.Admin), "Semi")[0];
        var (a, b) = (PlayerId(match, "playerA"), PlayerId(match, "playerB"));
        var bettor = t.Players.First(p => p.Id != a && p.Id != b);
        await BetOkAsync(bettor.Client, match, b, 15);

        await WinAsync(t.Admin, match, a);

        (await ToteTabAsync(t))[bettor.Login].Should().Be(0);
        (await TotalAsync(t.Admin, bettor.Login)).Should().Be(150);
    }

    [Fact]
    public async Task Undo_takes_payout_back_and_redo_pays_once()
    {
        var t = await SetupAsync(4);
        var match = Matches(await DetailsAsync(t.Admin), "Semi")[0];
        var (a, b) = (PlayerId(match, "playerA"), PlayerId(match, "playerB"));
        var bettor = t.Players.First(p => p.Id != a && p.Id != b);
        await BetOkAsync(bettor.Client, match, a, 20);
        await WinAsync(t.Admin, match, a);

        (await t.Admin.PostAsync($"/api/admin/matches/{MatchId(match)}/undo", null)).EnsureSuccessStatusCode();
        (await ToteTabAsync(t))[bettor.Login].Should().Be(-20);

        await WinAsync(t.Admin, match, a);
        (await ToteTabAsync(t))[bettor.Login].Should().Be(0);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DonkeyAwards.CountAsync(x => x.BetId != null && x.Amount > 0)).Should().Be(1);
    }

    [Fact]
    public async Task Details_show_pool_odds_and_my_bet()
    {
        var t = await SetupAsync(4);
        var match = Matches(await DetailsAsync(t.Admin), "Semi")[0];
        var (a, b) = (PlayerId(match, "playerA"), PlayerId(match, "playerB"));
        var others = t.Players.Where(p => p.Id != a && p.Id != b).ToList();
        await BetOkAsync(others[0].Client, match, a, 10);
        await BetOkAsync(others[1].Client, match, b, 20);

        var mine = Matches(await DetailsAsync(others[0].Client), "Semi")[0];

        mine.GetProperty("pool").GetInt32().Should().Be(30);
        mine.GetProperty("oddsA").GetDouble().Should().Be(3.0);
        mine.GetProperty("oddsB").GetDouble().Should().Be(1.5);
        mine.GetProperty("myBet").GetProperty("amount").GetInt32().Should().Be(10);
        mine.GetProperty("myBet").GetProperty("onPlayerId").GetGuid().Should().Be(a!.Value);
    }

    [Fact]
    public async Task Concurrent_bets_cannot_overdraw()
    {
        // Две ставки разом на текущий матч: пройдёт одна, баланс в минус не уйдёт.
        var t = await SetupAsync(8, startCapital: 30);
        var current = Matches(await DetailsAsync(t.Admin), "Quarter")[0];
        var bettor = t.Players.First(p => p.Id != PlayerId(current, "playerA") && p.Id != PlayerId(current, "playerB"));

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => BetAsync(bettor.Client, current, PlayerId(current, "playerA"), 25)));

        responses.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(1);
        (await TotalAsync(t.Admin, bettor.Login)).Should().Be(5);
    }

    [Fact]
    public async Task Bets_only_on_current_match()
    {
        // Тотализатор открыт только на текущий матч — первый по сетке несыгранный, где оба игрока известны.
        var t = await SetupAsync(4);
        var semis = Matches(await DetailsAsync(t.Admin), "Semi");
        var later = semis[1];
        var bettor = t.Players.First(p => p.Id != PlayerId(later, "playerA") && p.Id != PlayerId(later, "playerB"));

        var response = await BetAsync(bettor.Client, later, PlayerId(later, "playerA"), 10);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("not_current");
        (await DetailsAsync(bettor.Client)).GetProperty("betMatchId").GetGuid().Should().Be(MatchId(semis[0]));
    }

    [Fact]
    public async Task Current_match_started_bets_move_to_next()
    {
        // Пока идёт матч, ставят уже на следующий.
        var t = await SetupAsync(4);
        var semis = Matches(await DetailsAsync(t.Admin), "Semi");
        (await t.Admin.PostAsync($"/api/admin/matches/{MatchId(semis[0])}/start", null)).EnsureSuccessStatusCode();
        var bettor = t.Players.First(p => p.Id != PlayerId(semis[1], "playerA") && p.Id != PlayerId(semis[1], "playerB"));

        await BetOkAsync(bettor.Client, semis[1], PlayerId(semis[1], "playerA"), 10);
        (await DetailsAsync(bettor.Client)).GetProperty("betMatchId").GetGuid().Should().Be(MatchId(semis[1]));
    }

    [Fact]
    public async Task Bets_allowed_on_challenge_match()
    {
        var t = await SetupAsync(2);
        var final = Matches(await DetailsAsync(t.Admin), "Final").Single();
        var champion = t.Players.Single(p => p.Id == PlayerId(final, "playerA"));
        var other = t.Players.Single(p => p.Id != champion.Id);
        await WinAsync(t.Admin, final, champion.Id);
        (await champion.Client.PostAsJsonAsync($"/api/tournaments/{t.EventId}/challenge", new { accept = true })).EnsureSuccessStatusCode();
        var challenge = Matches(await DetailsAsync(t.Admin), "Challenge").Single();

        (await BetAsync(other.Client, challenge, champion.Id, 10)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public void Dispose() => _factory.Dispose();

    private record Player(Guid Id, string Login, HttpClient Client);

    private record Setup(Guid EventId, HttpClient Admin, List<Player> Players);

    private async Task<Setup> SetupAsync(int players, int startCapital = 150)
    {
        var list = new List<Player>();
        for (var i = 0; i < players; i++)
        {
            var login = $"p{i}";
            var id = await _factory.SeedPlayerAsync(login);
            await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(id, startCapital, DonkeyReasons.StartCapital));
            list.Add(new Player(id, login, await TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword)));
        }

        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Tournament", title = "FIFA" }, TestClients.Json);
        var eventId = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{eventId}/start", null)).EnsureSuccessStatusCode();
        return new Setup(eventId, admin, list);
    }

    private async Task<(JsonElement Match, Player Bettor)> OpenMatchAndBettorAsync(Setup t)
    {
        var match = Matches(await DetailsAsync(t.Admin), "Semi")[0];
        var bettor = t.Players.First(p => p.Id != PlayerId(match, "playerA") && p.Id != PlayerId(match, "playerB"));
        return (match, bettor);
    }

    private static Task<HttpResponseMessage> BetAsync(HttpClient client, JsonElement match, Guid? onPlayerId, int amount) =>
        client.PostAsJsonAsync($"/api/matches/{MatchId(match)}/bets", new { onPlayerId, amount });

    private static async Task BetOkAsync(HttpClient client, JsonElement match, Guid? onPlayerId, int amount) =>
        (await BetAsync(client, match, onPlayerId, amount)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task WinAsync(HttpClient admin, JsonElement match, Guid? winner) =>
        (await admin.PostAsJsonAsync($"/api/admin/matches/{MatchId(match)}/winner", new { winnerId = winner }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task<Dictionary<string, int>> ToteTabAsync(Setup t)
    {
        var events = await t.Admin.GetFromJsonAsync<List<EventDto>>("/api/events", TestClients.Json);
        var tote = events!.Single(e => e.Title == "Тотализатор: FIFA");
        var rows = await t.Admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={tote.Id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }

    private static async Task<int> TotalAsync(HttpClient admin, string login)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json);
        return rows!.Single(r => r.Login == login).Donkeys;
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static List<JsonElement> Matches(JsonElement details, string round) =>
        details.GetProperty("matches").EnumerateArray()
            .Where(m => m.GetProperty("round").GetString() == round)
            .OrderBy(m => m.GetProperty("roundIndex").GetInt32())
            .ToList();

    private static Guid MatchId(JsonElement match) => match.GetProperty("id").GetGuid();

    private static Guid? PlayerId(JsonElement match, string side) =>
        match.GetProperty(side).ValueKind == JsonValueKind.Null ? null : match.GetProperty(side).GetProperty("id").GetGuid();

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
}
