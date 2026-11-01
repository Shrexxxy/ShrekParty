using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Events;
using ShrekParty.Rehearsal;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Rehearsal;

public class BotTests : IAsyncDisposable
{
    private readonly AppFactory _factory = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationTokenSource _rerun = new();
    private readonly List<Task> _running = [];

    [Fact]
    public async Task Bots_register_and_connect()
    {
        await _factory.OpenRegistrationAsync();

        var bots = await RunBotsAsync(3);

        bots.Should().OnlyContain(b => b.UserId != null && b.Connected);
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/leaderboard"))!.Should().HaveCount(3);
    }

    [Fact]
    public async Task Bots_vote_in_poll()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);

        await StartAsync(admin, "FightPoll");

        await WaitUntilAsync(async () => (await DetailsAsync(admin)).GetProperty("options").EnumerateArray().Sum(o => o.GetProperty("votes").GetInt32()) == 3);
    }

    [Fact]
    public async Task Bots_upload_photo_in_hunt()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);

        await StartAsync(admin, "PhotoHunt");

        await WaitUntilAsync(async () => (await DetailsAsync(admin)).GetProperty("submitted").GetInt32() == 3);
    }

    [Fact]
    public async Task Bots_vote_for_hide_shot()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin, "HideAndSeek");
        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await admin.PostAsync($"/api/admin/hide/{id}/shots", new MultipartFormDataContent { { file, "file", "shot.jpg" }, { new StringContent(""), "userIds" } }))
            .EnsureSuccessStatusCode();

        (await admin.PostAsync($"/api/admin/hide/{id}/stop", null)).EnsureSuccessStatusCode();

        await WaitUntilAsync(async () => (await DetailsAsync(admin)).GetProperty("shots").EnumerateArray().Single().GetProperty("votes").GetInt32() == 3);
    }

    [Fact]
    public async Task Bots_play_tapper()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);
        await StartAsync(admin, "Tapper");
        _factory.Time.Advance(TimeSpan.FromSeconds(3));

        await WaitUntilAsync(async () =>
        {
            await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
            return (await DetailsAsync(admin)).GetProperty("rows").EnumerateArray().All(r => r.GetProperty("taps").GetInt32() > 0);
        });
    }

    [Fact]
    public async Task Bots_answer_days()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);

        await StartAsync(admin, "Days");

        await WaitUntilAsync(async () => (await DetailsAsync(admin)).GetProperty("answered").GetInt32() == 3);
    }

    [Fact]
    public async Task Bots_play_quiz_question()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);
        object Q(string text) => new { text, answer = "Ответ" };
        var content = new { themes = new object[] { new { round = 1, name = "Шрек", questions = new[] { Q("В1"), Q("В2"), Q("В3") } } } };
        (await admin.PostAsJsonAsync("/api/admin/quiz/import", content)).EnsureSuccessStatusCode();
        await StartAsync(admin, "Quiz");
        _factory.Time.Advance(TimeSpan.FromSeconds(5));

        // Рулетка → бот-выбирающий выбирает клетку → боты жмут → окно нажатий закрывается по часам.
        await WaitUntilAsync(async () =>
        {
            _factory.Time.Advance(TimeSpan.FromMilliseconds(200));
            await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
            return (await DetailsAsync(admin)).GetProperty("phase").GetString() == "Answering";
        });
    }

    [Fact]
    public async Task Bots_bid_in_auction()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(3);
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Кружка", startPrice = 10 })).EnsureSuccessStatusCode();
        var id = await StartAsync(admin, "Auction");

        (await admin.PostAsync($"/api/admin/auction/{id}/next", null)).EnsureSuccessStatusCode();

        await WaitUntilAsync(async () => (await DetailsAsync(admin)).GetProperty("leader").ValueKind == JsonValueKind.Object);
    }

    [Fact]
    public async Task Bots_play_mafia_day_and_night()
    {
        await _factory.OpenRegistrationAsync();
        await RunBotsAsync(6);
        var admin = await TestClients.AdminAsync(_factory);
        var id = await StartAsync(admin, "Mafia");

        // Ведущий жмёт «Дальше» только на знакомстве, утре и итогах; речи, голоса и ночь (с заданиями) боты делают сами.
        await WaitUntilAsync(async () =>
        {
            // Задания не чаще раза в 1,5 с по серверным часам — двигаем их; без тика фазы по таймеру не сменятся.
            _factory.Time.Advance(TimeSpan.FromSeconds(2));
            var details = await DetailsAsync(admin);
            var phase = details.GetProperty("phase").GetString();
            if (phase is "Intro" or "Morning" or "VoteResult" or "LastWord" && details.GetProperty("day").GetInt32() < 2)
            {
                await admin.PostAsync($"/api/admin/mafia/{id}/next", null);
            }

            return details.GetProperty("day").GetInt32() >= 2 || phase == "Ended";
        }, timeoutMs: 60_000);

        // Ночь кончилась досрочно, потому что мирные боты сделали задания, — ослики за них в копилке.
        (await DetailsAsync(admin)).GetProperty("bonuses").EnumerateArray()
            .Should().Contain(b => b.GetProperty("kind").GetString() == "Tasks" && b.GetProperty("count").GetInt32() == 5);
    }

    [Fact]
    public async Task Bot_rerun_logs_in_existing()
    {
        await _factory.OpenRegistrationAsync();
        var first = (await RunBotsAsync(1)).Single();
        await _stop.CancelAsync();
        await Task.WhenAll(_running);

        var again = NewBot(first.Login);
        _running.Add(again.RunAsync(_rerun.Token));
        await WaitUntilAsync(() => Task.FromResult(again.UserId != null && again.Connected));

        again.UserId.Should().Be(first.UserId!.Value);
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/leaderboard"))!.Should().HaveCount(1);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        await _rerun.CancelAsync();
        await Task.WhenAll(_running.Select(t => t.ContinueWith(_ => { })));
        _factory.Dispose();
    }

    private Bot NewBot(string login) =>
        new(login, new BotOptions(_factory.Server.BaseAddress, MinDelayMs: 0, MaxDelayMs: 0, PollMs: 50),
            new BotTransport(() => _factory.Server.CreateHandler()), TextWriter.Null, new Random(42));

    private async Task<List<Bot>> RunBotsAsync(int count)
    {
        var bots = BotNames.Take(count).Select(NewBot).ToList();
        _running.AddRange(bots.Select(b => b.RunAsync(_stop.Token)));
        await WaitUntilAsync(() => Task.FromResult(bots.All(b => b.UserId != null && b.Connected)));
        return bots;
    }

    private static async Task<Guid> StartAsync(HttpClient admin, string type)
    {
        // Пока регистрация открыта, она и есть текущий ивент.
        (await admin.PostAsJsonAsync("/api/admin/registration/close", new { })).EnsureSuccessStatusCode();
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type, title = type }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, int timeoutMs = 10_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!await condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "боты должны успеть");
            await Task.Delay(50);
        }
    }
}
