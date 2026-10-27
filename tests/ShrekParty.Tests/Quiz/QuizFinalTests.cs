using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Quiz;

public class QuizFinalTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Final_only_after_rounds()
    {
        var (id, admin) = await StartAsync();

        (await admin.PostAsync($"/api/admin/quiz/{id}/final", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Answer_of_last_question_stays_when_rounds_done()
    {
        var (_, admin) = await PlayRoundsAsync();

        var details = await DetailsAsync(await PlayerAsync("Cid"));
        details.GetProperty("phase").GetString().Should().Be("Done");
        details.GetProperty("answer").GetString().Should().NotBeNull();
    }

    [Fact]
    public async Task Final_only_positive()
    {
        var (id, admin) = await PlayRoundsAsync();

        await StartFinalAsync(admin, id);

        var final = (await DetailsAsync(admin)).GetProperty("final");
        final.GetProperty("themeName").GetString().Should().Be("Финал");
        final.GetProperty("finalists").EnumerateArray().Select(f => f.GetProperty("login").GetString()).Should().BeEquivalentTo(["Ann", "Bob"]);
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("FinalBetting");
    }

    [Fact]
    public async Task Question_hidden_while_betting()
    {
        var (id, admin) = await PlayRoundsAsync();
        await StartFinalAsync(admin, id);

        (await DetailsAsync(await PlayerAsync("Ann"))).GetProperty("question").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Bet_limits()
    {
        var (id, admin) = await PlayRoundsAsync();
        await StartFinalAsync(admin, id);
        var bob = await PlayerAsync("Bob");

        (await BetAsync(bob, id, 0)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BetAsync(bob, id, 101)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BetAsync(bob, id, 100)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BetAsync(await PlayerAsync("Cid"), id, 1)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await DetailsAsync(bob)).GetProperty("final").GetProperty("myBet").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Betting_deadline_starts_question()
    {
        var (id, admin) = await PlayRoundsAsync();
        await StartFinalAsync(admin, id);

        await AdvanceAsync(TimeSpan.FromSeconds(30));

        var details = await DetailsAsync(await PlayerAsync("Ann"));
        details.GetProperty("phase").GetString().Should().Be("FinalQuestion");
        details.GetProperty("question").GetProperty("text").GetString().Should().Be("Финальный");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(60));
        (await BetAsync(await PlayerAsync("Ann"), id, 5)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Answer_and_change()
    {
        var (id, admin) = await ToFinalQuestionAsync();
        var ann = await PlayerAsync("Ann");

        await AnswerOkAsync(ann, id, "Осёл");
        await AnswerOkAsync(ann, id, "Фиона");

        (await DetailsAsync(ann)).GetProperty("final").GetProperty("myAnswer").GetString().Should().Be("Фиона");
    }

    [Fact]
    public async Task Answer_after_deadline_409()
    {
        var (id, _) = await ToFinalQuestionAsync();
        _factory.Time.Advance(TimeSpan.FromSeconds(60));

        (await AnswerAsync(await PlayerAsync("Ann"), id, "Поздно")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Answers_hidden_from_players_until_reveal()
    {
        var (id, admin) = await ToFinalQuestionAsync();
        await AnswerOkAsync(await PlayerAsync("Ann"), id, "Секрет");
        await AdvanceAsync(TimeSpan.FromSeconds(60));

        var forBob = Finalist(await DetailsAsync(await PlayerAsync("Bob")), "Ann");
        forBob.GetProperty("answer").ValueKind.Should().Be(JsonValueKind.Null);
        forBob.GetProperty("bet").ValueKind.Should().Be(JsonValueKind.Null);
        Finalist(await DetailsAsync(admin), "Ann").GetProperty("answer").GetString().Should().Be("Секрет");
    }

    [Fact]
    public async Task Reveal_needs_all_judged()
    {
        var (id, admin) = await ToFinalQuestionAsync();
        await AnswerOkAsync(await PlayerAsync("Ann"), id, "Ответ");
        await AnswerOkAsync(await PlayerAsync("Bob"), id, "Ответ");
        await AdvanceAsync(TimeSpan.FromSeconds(60));
        await JudgeAsync(admin, id, "Ann", correct: true);

        (await admin.PostAsync($"/api/admin/quiz/{id}/reveal", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Judging_applies_bets_and_missing_is_wrong()
    {
        var (id, admin) = await PlayRoundsAsync();
        await StartFinalAsync(admin, id);
        await BetAsync(await PlayerAsync("Ann"), id, 150);
        // Bob не поставил — ставка 1; не ответил — неверно.
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        await AnswerOkAsync(await PlayerAsync("Ann"), id, "Верный");
        await AdvanceAsync(TimeSpan.FromSeconds(60));
        await JudgeAsync(admin, id, "Ann", correct: true);

        (await admin.PostAsync($"/api/admin/quiz/{id}/reveal", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(await PlayerAsync("Cid"));
        details.GetProperty("phase").GetString().Should().Be("FinalReveal");
        Scores(details).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 350, ["Bob"] = 99, ["Cid"] = 0 });
        Finalist(details, "Bob").GetProperty("bet").GetInt32().Should().Be(1);
        details.GetProperty("answer").GetString().Should().Be("Ответ Финальный");
    }

    [Fact]
    public async Task Reveal_order_low_to_high()
    {
        var (id, admin) = await ToFinalQuestionAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(60));
        await admin.PostAsync($"/api/admin/quiz/{id}/reveal", null);

        var finalists = (await DetailsAsync(admin)).GetProperty("final").GetProperty("finalists").EnumerateArray()
            .Select(f => f.GetProperty("login").GetString());

        finalists.Should().Equal("Bob", "Ann");
    }

    [Fact]
    public async Task No_positive_skips_to_reveal()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var (id, _) = await StartAsync(admin);
        await ChooseAsync(admin, id, 0);
        await CloseAsync(admin, id);
        await ChooseAsync(admin, id, 0);
        await CloseAsync(admin, id);

        await StartFinalAsync(admin, id);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("FinalReveal");
        details.GetProperty("final").GetProperty("finalists").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Rank_includes_final()
    {
        var (id, admin) = await PlayRoundsAsync();
        await StartFinalAsync(admin, id);
        await BetAsync(await PlayerAsync("Ann"), id, 200);
        await BetAsync(await PlayerAsync("Bob"), id, 100);
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        await AnswerOkAsync(await PlayerAsync("Bob"), id, "Верный");
        await AnswerOkAsync(await PlayerAsync("Ann"), id, "Мимо");
        await AdvanceAsync(TimeSpan.FromSeconds(60));
        await JudgeAsync(admin, id, "Bob", correct: true);
        await JudgeAsync(admin, id, "Ann", correct: false);
        await admin.PostAsync($"/api/admin/quiz/{id}/reveal", null);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        // Bob 200, Ann 0, Cid 0: два места.
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Bob"] = 100, ["Ann"] = 50, ["Cid"] = 50 });
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>Раунд 1: «Шрек» (Ш100, Ш200); финал «Финал».</summary>
    private async Task<(Guid Id, HttpClient Admin)> StartAsync(HttpClient? admin = null)
    {
        foreach (var login in new[] { "Ann", "Bob", "Cid" })
        {
            await _factory.SeedPlayerAsync(login);
        }

        admin ??= await TestClients.AdminAsync(_factory);
        object Q(string text) => new { text, answer = "Ответ " + text };
        var content = new
        {
            themes = new object[]
            {
                new { round = 1, name = "Шрек", questions = new[] { Q("Ш100"), Q("Ш200") } },
                new { round = 0, name = "Финал", questions = new[] { Q("Финальный") } },
            },
        };
        (await admin.PostAsJsonAsync("/api/admin/quiz/import", content)).EnsureSuccessStatusCode();
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Quiz", title = "Своя игра" }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        await AdvanceAsync(TimeSpan.FromSeconds(5));
        return (id, admin);
    }

    /// <summary>Ann верно на Ш200 (+200), Bob верно на Ш100 (+100), Cid — 0; раунды сыграны.</summary>
    private async Task<(Guid Id, HttpClient Admin)> PlayRoundsAsync()
    {
        var (id, admin) = await StartAsync();
        await ChooseAsync(admin, id, 1);
        await PressAloneAsync(id, "Ann");
        await VerdictAsync(admin, id, true);
        await ChooseAsync(admin, id, 0);
        await PressAloneAsync(id, "Bob");
        await VerdictAsync(admin, id, true);
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> ToFinalQuestionAsync()
    {
        var (id, admin) = await PlayRoundsAsync();
        await StartFinalAsync(admin, id);
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        return (id, admin);
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    /// <summary>Админ выбирает клетку первой темы по номеру среди всех клеток (сыгранные тоже на табло).</summary>
    private static async Task ChooseAsync(HttpClient admin, Guid id, int cell)
    {
        var cells = (await DetailsAsync(admin)).GetProperty("board")[0].GetProperty("cells").EnumerateArray()
            .Where(c => !c.GetProperty("played").GetBoolean()).ToList();
        var questionId = cells.Count == 2 ? cells[cell].GetProperty("questionId").GetGuid() : cells[0].GetProperty("questionId").GetGuid();
        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/choose", new { questionId })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task PressAloneAsync(Guid id, string login)
    {
        var press = (await PlayerAsync(login)).PostAsJsonAsync($"/api/quiz/{id}/press", new { clientTime = _factory.Time.GetUtcNow() });
        await Task.Delay(200);
        _factory.Time.Advance(TimeSpan.FromMilliseconds(150));
        (await press).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task VerdictAsync(HttpClient admin, Guid id, bool correct) =>
        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/verdict", new { correct })).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task CloseAsync(HttpClient admin, Guid id) =>
        (await admin.PostAsync($"/api/admin/quiz/{id}/close", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task StartFinalAsync(HttpClient admin, Guid id) =>
        (await admin.PostAsync($"/api/admin/quiz/{id}/final", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static Task<HttpResponseMessage> BetAsync(HttpClient player, Guid id, int amount) =>
        player.PostAsJsonAsync($"/api/quiz/{id}/bet", new { amount });

    private static Task<HttpResponseMessage> AnswerAsync(HttpClient player, Guid id, string text) =>
        player.PostAsJsonAsync($"/api/quiz/{id}/final-answer", new { text });

    private static async Task AnswerOkAsync(HttpClient player, Guid id, string text) =>
        (await AnswerAsync(player, id, text)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task JudgeAsync(HttpClient admin, Guid id, string login, bool correct)
    {
        var userId = Finalist(await DetailsAsync(admin), login).GetProperty("userId").GetGuid();
        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/final-verdict", new { userId, correct })).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static JsonElement Finalist(JsonElement details, string login) =>
        details.GetProperty("final").GetProperty("finalists").EnumerateArray().Single(f => f.GetProperty("login").GetString() == login);

    private static Dictionary<string, int> Scores(JsonElement details) =>
        details.GetProperty("scores").EnumerateArray().ToDictionary(s => s.GetProperty("login").GetString()!, s => s.GetProperty("score").GetInt32());

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid id)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }
}
