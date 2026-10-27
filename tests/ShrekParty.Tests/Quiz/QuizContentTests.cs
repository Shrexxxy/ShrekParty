using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Quiz;

public class QuizContentTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Create_theme_and_questions_with_default_prices()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var theme = await ThemeAsync(admin, round: 2, "Шрек");

        await QuestionAsync(admin, theme, "Как зовут осла?", "Осёл");
        await QuestionAsync(admin, theme, "Кто жена Шрека?", "Фиона");

        var questions = Themes(await TreeAsync(admin)).Single().GetProperty("questions").EnumerateArray().ToList();
        questions.Select(q => q.GetProperty("price").GetInt32()).Should().Equal(200, 400);
        questions[0].GetProperty("answer").GetString().Should().Be("Осёл");
    }

    [Fact]
    public async Task Explicit_price_kept()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var theme = await ThemeAsync(admin, round: 1, "Шрек");

        await QuestionAsync(admin, theme, "Вопрос", "Ответ", price: 777);

        Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0].GetProperty("price").GetInt32().Should().Be(777);
    }

    [Fact]
    public async Task Edit_and_delete()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var theme = await ThemeAsync(admin, round: 1, "Шрек");
        var question = await QuestionAsync(admin, theme, "Вопрос", "Ответ");

        (await admin.PutAsJsonAsync($"/api/admin/quiz/themes/{theme}", new { name = "Осёл" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.PutAsJsonAsync($"/api/admin/quiz/questions/{question}", new { text = "Новый", answer = "Новый ответ", price = 300 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var saved = Themes(await TreeAsync(admin)).Single();
        saved.GetProperty("name").GetString().Should().Be("Осёл");
        saved.GetProperty("questions")[0].GetProperty("text").GetString().Should().Be("Новый");
        saved.GetProperty("questions")[0].GetProperty("price").GetInt32().Should().Be(300);

        (await admin.DeleteAsync($"/api/admin/quiz/questions/{question}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.DeleteAsync($"/api/admin/quiz/themes/{theme}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Themes(await TreeAsync(admin)).Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_text_400()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var theme = await ThemeAsync(admin, round: 1, "Шрек");

        (await admin.PostAsJsonAsync($"/api/admin/quiz/themes/{theme}/questions", new { text = " ", answer = "Ответ" }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Final_has_one_question()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var final = await ThemeAsync(admin, round: 0, "Финал");
        await QuestionAsync(admin, final, "Вопрос финала", "Ответ");

        (await admin.PostAsJsonAsync($"/api/admin/quiz/themes/{final}/questions", new { text = "Второй", answer = "Нет" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/admin/quiz/themes", new { round = 0, name = "Ещё финал" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("image/jpeg", "Image")]
    [InlineData("audio/mpeg", "Audio")]
    [InlineData("video/mp4", "Video")]
    public async Task Media_upload_sets_type(string contentType, string mediaType)
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");

        (await UploadAsync(admin, question, contentType, 3)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var saved = Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0];
        saved.GetProperty("mediaType").GetString().Should().Be(mediaType);
        saved.GetProperty("mediaUrl").GetString().Should().StartWith("/media/");
    }

    [Fact]
    public async Task Media_bad_type_400()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");

        (await UploadAsync(admin, question, "application/pdf", 3)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Media_over_old_10mb_limit_allowed()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");

        (await UploadAsync(admin, question, "video/mp4", 20 * 1024 * 1024)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Media_remove()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");
        await UploadAsync(admin, question, "image/jpeg", 3);

        (await admin.DeleteAsync($"/api/admin/quiz/questions/{question}/media")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0].GetProperty("mediaUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Video_served_with_ranges()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");
        await UploadAsync(admin, question, "video/mp4", 1000);
        var url = Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0].GetProperty("mediaUrl").GetString();

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Range = new RangeHeaderValue(100, 199);
        var response = await admin.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        (await response.Content.ReadAsByteArrayAsync()).Should().HaveCount(100);
    }

    [Fact]
    public async Task Import_replaces_and_export_roundtrips()
    {
        var admin = await TestClients.AdminAsync(_factory);
        await ThemeAsync(admin, round: 1, "Старая тема");
        var content = new
        {
            themes = new object[]
            {
                new { round = 1, name = "Шрек", questions = new object[] { new { text = "В1", answer = "О1" }, new { text = "В2", answer = "О2", price = 250 } } },
                new { round = 2, name = "Осёл", questions = new object[] { new { text = "В3", answer = "О3" } } },
                new { round = 0, name = "Финал", questions = new object[] { new { text = "ВФ", answer = "ОФ" } } },
            },
        };

        (await admin.PostAsJsonAsync("/api/admin/quiz/import", content)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var themes = Themes(await TreeAsync(admin));
        themes.Select(t => t.GetProperty("name").GetString()).Should().BeEquivalentTo(["Шрек", "Осёл", "Финал"]);
        themes.Single(t => t.GetProperty("name").GetString() == "Шрек").GetProperty("questions").EnumerateArray()
            .Select(q => q.GetProperty("price").GetInt32()).Should().Equal(100, 250);

        var exported = await admin.GetFromJsonAsync<JsonElement>("/api/admin/quiz/export");
        (await admin.PostAsJsonAsync("/api/admin/quiz/import", exported)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        Themes(await TreeAsync(admin)).Should().HaveCount(3);
    }

    [Fact]
    public async Task Import_removes_old_media()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");
        await UploadAsync(admin, question, "image/jpeg", 3);
        var oldUrl = Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0].GetProperty("mediaUrl").GetString();

        var content = new { themes = new object[] { new { round = 1, name = "Новая", questions = new object[] { new { text = "В", answer = "О" } } } } };
        (await admin.PostAsJsonAsync("/api/admin/quiz/import", content)).EnsureSuccessStatusCode();

        (await admin.GetAsync(oldUrl)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Bad_import_keeps_old_content_and_media()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");
        await UploadAsync(admin, question, "image/jpeg", 3);
        var oldUrl = Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0].GetProperty("mediaUrl").GetString();

        var broken = new { themes = new object[] { new { round = 1, name = "Новая", questions = new object[] { new { text = "В", answer = " " } } } } };
        (await admin.PostAsJsonAsync("/api/admin/quiz/import", broken)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        Themes(await TreeAsync(admin)).Single().GetProperty("name").GetString().Should().Be("Шрек");
        (await admin.GetAsync(oldUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Import_two_finals_400()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var content = new
        {
            themes = new object[]
            {
                new { round = 0, name = "Ф1", questions = new object[] { new { text = "В", answer = "О" } } },
                new { round = 0, name = "Ф2", questions = new object[] { new { text = "В", answer = "О" } } },
            },
        };

        (await admin.PostAsJsonAsync("/api/admin/quiz/import", content)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reset_keeps_quiz()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var question = await QuestionAsync(admin, await ThemeAsync(admin, round: 1, "Шрек"), "Вопрос", "Ответ");
        await UploadAsync(admin, question, "image/jpeg", 3);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        var saved = Themes(await TreeAsync(admin)).Single().GetProperty("questions")[0];
        saved.GetProperty("mediaUrl").GetString().Should().StartWith("/media/");
        (await admin.GetAsync(saved.GetProperty("mediaUrl").GetString())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Player_cannot_edit()
    {
        await _factory.SeedPlayerAsync("Ann");
        var ann = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);

        (await ann.GetAsync("/api/admin/quiz")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> ThemeAsync(HttpClient admin, int round, string name)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/quiz/themes", new { round, name });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<Guid> QuestionAsync(HttpClient admin, Guid theme, string text, string answer, int? price = null)
    {
        var response = await admin.PostAsJsonAsync($"/api/admin/quiz/themes/{theme}/questions", new { text, answer, price });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient admin, Guid question, string contentType, int size)
    {
        var file = new ByteArrayContent(new byte[size]);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return admin.PostAsync($"/api/admin/quiz/questions/{question}/media", new MultipartFormDataContent { { file, "file", "media.bin" } });
    }

    private static async Task<JsonElement> TreeAsync(HttpClient admin) => await admin.GetFromJsonAsync<JsonElement>("/api/admin/quiz");

    private static List<JsonElement> Themes(JsonElement tree) => tree.GetProperty("themes").EnumerateArray().ToList();
}
