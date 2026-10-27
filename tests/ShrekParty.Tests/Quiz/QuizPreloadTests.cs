using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Live;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Quiz;

public class QuizPreloadTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Quiz_media_list_for_viewer()
    {
        var admin = await TestClients.AdminAsync(_factory);
        await QuestionWithMediaAsync(admin, "video/mp4");
        await QuestionWithMediaAsync(admin, "image/jpeg");
        await QuestionWithMediaAsync(admin, null);

        var media = (await (await TestClients.ViewerAsync(_factory)).GetFromJsonAsync<List<JsonElement>>("/api/tv/quiz-media"))!;

        media.Should().HaveCount(2);
        media.Select(m => m.GetProperty("type").GetString()).Should().BeEquivalentTo(["Video", "Image"]);
        media.Should().OnlyContain(m => m.GetProperty("url").GetString()!.StartsWith("/media/"));
    }

    [Fact]
    public async Task Player_cannot_get_quiz_media()
    {
        await _factory.SeedPlayerAsync("Ann");
        var ann = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);

        (await ann.GetAsync("/api/tv/quiz-media")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Preload_status_in_admin_snapshot_only()
    {
        var viewer = await TestClients.ViewerAsync(_factory);

        (await viewer.PostAsJsonAsync("/api/tv/preload", new { loaded = 3, total = 5 })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var forAdmin = await SnapshotAsync(new SnapshotAudience(SignedIn: true, IsAdmin: true));
        forAdmin.TvPreload!.Loaded.Should().Be(3);
        forAdmin.TvPreload.Total.Should().Be(5);
        (await SnapshotAsync(new SnapshotAudience(SignedIn: true))).TvPreload.Should().BeNull();
    }

    [Fact]
    public async Task Bad_preload_400()
    {
        var viewer = await TestClients.ViewerAsync(_factory);

        (await viewer.PostAsJsonAsync("/api/tv/preload", new { loaded = 6, total = 5 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose() => _factory.Dispose();

    private static async Task QuestionWithMediaAsync(HttpClient admin, string? contentType)
    {
        var tree = await admin.GetFromJsonAsync<JsonElement>("/api/admin/quiz");
        var theme = tree.GetProperty("themes").EnumerateArray().FirstOrDefault();
        Guid themeId;
        if (theme.ValueKind == JsonValueKind.Undefined)
        {
            var created = await admin.PostAsJsonAsync("/api/admin/quiz/themes", new { round = 1, name = "Шрек" });
            themeId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        }
        else
        {
            themeId = theme.GetProperty("id").GetGuid();
        }

        var question = await admin.PostAsJsonAsync($"/api/admin/quiz/themes/{themeId}/questions", new { text = "Вопрос", answer = "Ответ" });
        var questionId = (await question.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        if (contentType is not null)
        {
            var file = new ByteArrayContent(new byte[10]);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            (await admin.PostAsync($"/api/admin/quiz/questions/{questionId}/media", new MultipartFormDataContent { { file, "file", "m" } })).EnsureSuccessStatusCode();
        }
    }

    private async Task<AppSnapshot> SnapshotAsync(SnapshotAudience audience)
    {
        AppSnapshot? snapshot = null;
        await _factory.WithServiceAsync<SnapshotBuilder>(async b => snapshot = await b.BuildAsync(audience));
        return snapshot!;
    }
}
