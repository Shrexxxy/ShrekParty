using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Quiz;

public class QuizGameTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_roulette_then_choosing()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (_, admin) = await StartAsync();

        var roulette = await DetailsAsync(admin);
        roulette.GetProperty("phase").GetString().Should().Be("Roulette");
        roulette.GetProperty("round").GetInt32().Should().Be(1);
        roulette.GetProperty("chooserId").ValueKind.Should().Be(JsonValueKind.String);

        await AdvanceAsync(TimeSpan.FromSeconds(5));

        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Choosing");
    }

    [Fact]
    public async Task Start_without_questions_409()
    {
        await SeedPlayersAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Edit_during_game_409()
    {
        await SeedPlayersAsync("Ann");
        var (_, admin) = await StartAsync();

        (await admin.PostAsJsonAsync("/api/admin/quiz/themes", new { round = 3, name = "Ещё" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Board_shows_current_round()
    {
        await SeedPlayersAsync("Ann");
        var (_, admin) = await StartChoosingAsync();

        var board = (await DetailsAsync(admin)).GetProperty("board").EnumerateArray().ToList();

        board.Select(t => t.GetProperty("name").GetString()).Should().Equal("Шрек", "Осёл");
        board[0].GetProperty("cells").EnumerateArray().Select(c => c.GetProperty("price").GetInt32()).Should().Equal(100, 200);
    }

    [Fact]
    public async Task Only_chooser_can_choose()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartChoosingAsync();
        var (chooser, other) = await ChooserAndOtherAsync(admin);

        (await ChooseAsync(other, id, await CellAsync(admin, 0, 0))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ChooseAsync(chooser, id, await CellAsync(admin, 0, 0))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Reading");
        details.GetProperty("question").GetProperty("text").GetString().Should().Be("Ш100");
        details.GetProperty("readingStartedAt").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow());
    }

    [Fact]
    public async Task Admin_chooses_for_player()
    {
        await SeedPlayersAsync("Ann");
        var (id, admin) = await StartChoosingAsync();

        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/choose", new { questionId = await CellAsync(admin, 1, 1) })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DetailsAsync(admin)).GetProperty("question").GetProperty("text").GetString().Should().Be("О200");
    }

    [Fact]
    public async Task Played_cell_409()
    {
        await SeedPlayersAsync("Ann");
        var (id, admin) = await StartChoosingAsync();
        var cell = await CellAsync(admin, 0, 0);
        await AdminChooseAsync(admin, id, cell);
        await CloseAsync(admin, id);

        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/choose", new { questionId = cell })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailsAsync(admin)).GetProperty("board")[0].GetProperty("cells")[0].GetProperty("played").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Press_earliest_wins_within_window()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync();
        var start = _factory.Time.GetUtcNow();
        var (ann, bob) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"));

        // Ann пришла первой по сети, но Bob нажал раньше по часам — побеждает Bob. Запросы приходят после нажатий.
        _factory.Time.Advance(TimeSpan.FromMilliseconds(400));
        var first = PressAsync(ann, id, start.AddMilliseconds(300));
        await Task.Delay(200);
        (await PressAsync(bob, id, start.AddMilliseconds(200))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        _factory.Time.Advance(TimeSpan.FromMilliseconds(150));
        (await first).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Answering");
        Login(details, details.GetProperty("answererId").GetGuid()).Should().Be("Bob");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(10));
    }

    [Fact]
    public async Task Press_before_reading_counts_as_start()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync();
        var start = _factory.Time.GetUtcNow();
        var (ann, bob) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"));

        // Часы Ann спешат: «нажала» до начала вопроса — считается началом, а не раньше всех на 5 секунд.
        var first = PressAsync(bob, id, start);
        await Task.Delay(200);
        await PressAsync(ann, id, start.AddSeconds(-5));
        _factory.Time.Advance(TimeSpan.FromMilliseconds(150));
        await first;

        // Обе — в момент начала; при равенстве побеждает пришедший первым.
        var details = await DetailsAsync(admin);
        Login(details, details.GetProperty("answererId").GetGuid()).Should().Be("Bob");
    }

    [Fact]
    public async Task Tick_resolves_window_if_request_lost()
    {
        await SeedPlayersAsync("Ann");
        var (id, admin) = await StartReadingAsync();
        var start = _factory.Time.GetUtcNow();

        _ = PressAsync(await PlayerAsync("Ann"), id, start);
        await Task.Delay(200);
        await AdvanceAsync(TimeSpan.FromSeconds(1));

        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Answering");
    }

    [Fact]
    public async Task Correct_adds_price_and_passes_choice()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync(row: 0, cell: 1);
        await PressAloneAsync(id, "Bob");

        (await VerdictAsync(admin, id, correct: true)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Closed");
        Login(details, details.GetProperty("chooserId").GetGuid()).Should().Be("Bob");
        Scores(details)["Bob"].Should().Be(200);
    }

    [Fact]
    public async Task Wrong_subtracts_and_reopens()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync(row: 0, cell: 1);
        await PressAloneAsync(id, "Bob");

        await VerdictAsync(admin, id, correct: false);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Reading");
        Scores(details)["Bob"].Should().Be(-200);
        details.GetProperty("wrong").EnumerateArray().Select(w => Login(details, w.GetGuid())).Should().Equal("Bob");
    }

    [Fact]
    public async Task Wrong_player_cannot_press_again()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync();
        await PressAloneAsync(id, "Bob");
        await VerdictAsync(admin, id, correct: false);

        (await PressAsync(await PlayerAsync("Bob"), id, _factory.Time.GetUtcNow())).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Others_press_again_after_wrong()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync();
        await PressAloneAsync(id, "Bob");
        await VerdictAsync(admin, id, correct: false);

        await PressAloneAsync(id, "Ann");

        var details = await DetailsAsync(admin);
        Login(details, details.GetProperty("answererId").GetGuid()).Should().Be("Ann");
    }

    [Fact]
    public async Task Answer_timeout_is_wrong()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync();
        await PressAloneAsync(id, "Bob");

        await AdvanceAsync(TimeSpan.FromSeconds(10));

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Reading");
        Scores(details)["Bob"].Should().Be(-100);
    }

    [Fact]
    public async Task Close_question_reveals_answer_and_keeps_chooser()
    {
        await SeedPlayersAsync("Ann", "Bob");
        var (id, admin) = await StartReadingAsync();
        var chooser = (await DetailsAsync(admin)).GetProperty("chooserId").GetGuid();

        await CloseAsync(admin, id);

        var details = await DetailsAsync(await PlayerAsync("Ann"));
        details.GetProperty("phase").GetString().Should().Be("Closed");
        details.GetProperty("answer").GetString().Should().Be("Ответ Ш100");
        details.GetProperty("chooserId").GetGuid().Should().Be(chooser);
    }

    [Fact]
    public async Task Answer_hidden_from_players_until_closed()
    {
        await SeedPlayersAsync("Ann");
        var (_, admin) = await StartReadingAsync();

        (await DetailsAsync(await PlayerAsync("Ann"))).GetProperty("answer").ValueKind.Should().Be(JsonValueKind.Null);
        (await DetailsAsync(admin)).GetProperty("answer").GetString().Should().Be("Ответ Ш100");
    }

    [Fact]
    public async Task Board_empty_moves_to_next_round()
    {
        await SeedPlayersAsync("Ann");
        var (id, admin) = await StartChoosingAsync();
        for (var row = 0; row < 2; row++)
        {
            for (var cell = 0; cell < 2; cell++)
            {
                await AdminChooseAsync(admin, id, await CellAsync(admin, row, cell));
                await CloseAsync(admin, id);
            }
        }

        var details = await DetailsAsync(admin);
        details.GetProperty("round").GetInt32().Should().Be(2);
        details.GetProperty("board").EnumerateArray().Select(t => t.GetProperty("name").GetString()).Should().Equal("Фиона");
    }

    [Fact]
    public async Task Next_round_by_admin()
    {
        await SeedPlayersAsync("Ann");
        var (id, admin) = await StartChoosingAsync();

        (await admin.PostAsync($"/api/admin/quiz/{id}/next-round", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DetailsAsync(admin)).GetProperty("round").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Media_url_only_tv_admin_for_video()
    {
        await SeedPlayersAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await ImportAsync(admin);
        var video = await CellAsync(admin, 0, 0, fromTree: true);
        var file = new ByteArrayContent(new byte[10]);
        file.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        (await admin.PostAsync($"/api/admin/quiz/questions/{video}/media", new MultipartFormDataContent { { file, "file", "v.mp4" } })).EnsureSuccessStatusCode();
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        await AdvanceAsync(TimeSpan.FromSeconds(5));
        await AdminChooseAsync(admin, id, video);

        (await DetailsAsync(admin)).GetProperty("question").GetProperty("mediaUrl").GetString().Should().StartWith("/media/");
        (await DetailsAsync(await TestClients.ViewerAsync(_factory))).GetProperty("question").GetProperty("mediaUrl").GetString().Should().StartWith("/media/");
        var phone = (await DetailsAsync(await PlayerAsync("Ann"))).GetProperty("question");
        phone.GetProperty("mediaType").GetString().Should().Be("Video");
        phone.GetProperty("mediaUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Media_command_counts_up()
    {
        await SeedPlayersAsync("Ann");
        var (id, admin) = await StartReadingAsync();

        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/media", new { action = "Pause" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/media", new { action = "Play" });

        var command = (await DetailsAsync(admin)).GetProperty("mediaCommand");
        command.GetProperty("action").GetString().Should().Be("Play");
        command.GetProperty("seq").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Rank_by_score_including_negative()
    {
        await SeedPlayersAsync("Ann", "Bob", "Cid");
        var (id, admin) = await StartReadingAsync(row: 0, cell: 1);
        await PressAloneAsync(id, "Bob");
        await VerdictAsync(admin, id, correct: false);
        await PressAloneAsync(id, "Ann");
        await VerdictAsync(admin, id, correct: true);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        // Ann +200, Cid 0, Bob −200: три места, Linear до 100.
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 100, ["Cid"] = 67, ["Bob"] = 33 });
    }

    public void Dispose() => _factory.Dispose();

    private async Task SeedPlayersAsync(params string[] logins)
    {
        foreach (var login in logins)
        {
            await _factory.SeedPlayerAsync(login);
        }
    }

    /// <summary>Раунд 1: «Шрек» (Ш100, Ш200), «Осёл» (О100, О200); раунд 2: «Фиона» (Ф200); финал.</summary>
    private static async Task ImportAsync(HttpClient admin)
    {
        object Q(string text) => new { text, answer = "Ответ " + text };
        var content = new
        {
            themes = new object[]
            {
                new { round = 1, name = "Шрек", questions = new[] { Q("Ш100"), Q("Ш200") } },
                new { round = 1, name = "Осёл", questions = new[] { Q("О100"), Q("О200") } },
                new { round = 2, name = "Фиона", questions = new[] { Q("Ф200") } },
                new { round = 0, name = "Финал", questions = new[] { Q("Финальный") } },
            },
        };
        (await admin.PostAsJsonAsync("/api/admin/quiz/import", content)).EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Quiz", title = "Своя игра" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        await ImportAsync(admin);
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> StartChoosingAsync()
    {
        var started = await StartAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(5));
        return started;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartReadingAsync(int row = 0, int cell = 0)
    {
        var (id, admin) = await StartChoosingAsync();
        await AdminChooseAsync(admin, id, await CellAsync(admin, row, cell));
        return (id, admin);
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private async Task<(HttpClient Chooser, HttpClient Other)> ChooserAndOtherAsync(HttpClient admin)
    {
        var details = await DetailsAsync(admin);
        var chooser = Login(details, details.GetProperty("chooserId").GetGuid());
        var other = chooser == "Ann" ? "Bob" : "Ann";
        return (await PlayerAsync(chooser), await PlayerAsync(other));
    }

    private static async Task<Guid> CellAsync(HttpClient admin, int row, int cell, bool fromTree = false)
    {
        if (fromTree)
        {
            var tree = await admin.GetFromJsonAsync<JsonElement>("/api/admin/quiz");
            return tree.GetProperty("themes")[row].GetProperty("questions")[cell].GetProperty("id").GetGuid();
        }

        return (await DetailsAsync(admin)).GetProperty("board")[row].GetProperty("cells")[cell].GetProperty("questionId").GetGuid();
    }

    private static Task<HttpResponseMessage> ChooseAsync(HttpClient player, Guid id, Guid questionId) =>
        player.PostAsJsonAsync($"/api/quiz/{id}/choose", new { questionId });

    private static async Task AdminChooseAsync(HttpClient admin, Guid id, Guid questionId) =>
        (await admin.PostAsJsonAsync($"/api/admin/quiz/{id}/choose", new { questionId })).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static Task<HttpResponseMessage> PressAsync(HttpClient player, Guid id, DateTimeOffset clientTime) =>
        player.PostAsJsonAsync($"/api/quiz/{id}/press", new { clientTime });

    /// <summary>Единственное нажатие: окно 150 мс проходит, игрок отвечает.</summary>
    private async Task PressAloneAsync(Guid id, string login)
    {
        var press = PressAsync(await PlayerAsync(login), id, _factory.Time.GetUtcNow());
        await Task.Delay(200);
        _factory.Time.Advance(TimeSpan.FromMilliseconds(150));
        (await press).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> VerdictAsync(HttpClient admin, Guid id, bool correct) =>
        admin.PostAsJsonAsync($"/api/admin/quiz/{id}/verdict", new { correct });

    private static async Task CloseAsync(HttpClient admin, Guid id) =>
        (await admin.PostAsync($"/api/admin/quiz/{id}/close", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static string Login(JsonElement details, Guid userId) =>
        details.GetProperty("scores").EnumerateArray().Single(s => s.GetProperty("userId").GetGuid() == userId).GetProperty("login").GetString()!;

    private static Dictionary<string, int> Scores(JsonElement details) =>
        details.GetProperty("scores").EnumerateArray().ToDictionary(s => s.GetProperty("login").GetString()!, s => s.GetProperty("score").GetInt32());

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid id)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }
}
