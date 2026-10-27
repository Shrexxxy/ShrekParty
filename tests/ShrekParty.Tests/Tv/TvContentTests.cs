using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Events;
using ShrekParty.Api.Live;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Tv;

public class TvContentTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Message_in_snapshot_until_expires()
    {
        var admin = await TestClients.AdminAsync(_factory);

        await PostMessageOkAsync(admin, "Торт через 5 минут!", seconds: 30);

        var snapshot = await SnapshotAsync();
        snapshot.TvMessage!.Text.Should().Be("Торт через 5 минут!");
        snapshot.TvMessage.Until.Should().Be(_factory.Time.GetUtcNow().AddSeconds(30));

        _factory.Time.Advance(TimeSpan.FromSeconds(31));
        (await SnapshotAsync()).TvMessage.Should().BeNull();
    }

    [Fact]
    public async Task Message_with_image()
    {
        var admin = await TestClients.AdminAsync(_factory);

        await PostMessageOkAsync(admin, "Смотрите!", seconds: 10, withImage: true);

        (await SnapshotAsync()).TvMessage!.ImageUrl.Should().StartWith("/media/");
    }

    [Fact]
    public async Task New_message_replaces_old_and_delete_hides()
    {
        var admin = await TestClients.AdminAsync(_factory);
        await PostMessageOkAsync(admin, "Первое", seconds: 60);
        await PostMessageOkAsync(admin, "Второе", seconds: 60);
        (await SnapshotAsync()).TvMessage!.Text.Should().Be("Второе");

        (await admin.DeleteAsync("/api/admin/tv/message")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SnapshotAsync()).TvMessage.Should().BeNull();
    }

    [Fact]
    public async Task Empty_message_400()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var response = await admin.PostAsync("/api/admin/tv/message", Form(text: " ", seconds: 10));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Player_cannot_post_message()
    {
        await _factory.SeedPlayerAsync("Ann");
        var player = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);

        var response = await player.PostAsync("/api/admin/tv/message", Form(text: "Хи-хи", seconds: 10));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Captions_seeded_once()
    {
        using (var first = new AppFactory(_factory.DataDir))
        {
            first.CreateClient();
        }

        using var second = new AppFactory(_factory.DataDir);
        var admin = await TestClients.AdminAsync(second);

        var captions = await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/captions");
        captions!.Should().HaveCount(5);
        captions!.Select(c => c.GetProperty("text").GetString()).Should().Contain("ФОТО ЖОСКОЙ ЗАРУБЫ {A} И {B}");
    }

    [Fact]
    public async Task Captions_add_and_delete()
    {
        var admin = await TestClients.AdminAsync(_factory);

        var created = await admin.PostAsJsonAsync("/api/admin/captions", new { text = "{A} УНИЧТОЖАЕТ {B}" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/captions"))!.Should().HaveCount(6);

        (await admin.DeleteAsync($"/api/admin/captions/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/captions"))!.Should().HaveCount(5);
    }

    [Fact]
    public async Task Slides_include_match_photo_with_caption()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        var tournament = await CreateAsync(admin, "Tournament", "FIFA");
        (await admin.PostAsync($"/api/admin/events/{tournament}/start", null)).EnsureSuccessStatusCode();
        var final = (await admin.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details").GetProperty("matches")[0];
        (await admin.PostAsync($"/api/admin/matches/{final.GetProperty("id").GetGuid()}/photos", ImageForm())).EnsureSuccessStatusCode();

        var slide = (await SlidesAsync()).Single(s => s.GetProperty("kind").GetString() == "Photo");

        slide.GetProperty("imageUrl").GetString().Should().StartWith("/media/");
        slide.GetProperty("logins").EnumerateArray().Select(l => l.GetString()).Should().BeEquivalentTo("Ann", "Bob");
        var text = slide.GetProperty("text").GetString()!;
        text.Should().Contain("Ann").And.Contain("Bob").And.NotContain("{A}");
    }

    [Fact]
    public async Task Slides_include_memes_with_caption()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var form = ImageForm();
        form.Add(new StringContent("Осёл в шоке"), "caption");
        (await admin.PostAsync("/api/admin/memes", form)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var slide = (await SlidesAsync()).Single();

        slide.GetProperty("kind").GetString().Should().Be("Photo");
        slide.GetProperty("text").GetString().Should().Be("Осёл в шоке");
    }

    [Fact]
    public async Task Meme_delete_removes_slide()
    {
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsync("/api/admin/memes", ImageForm())).EnsureSuccessStatusCode();
        var meme = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/memes"))!.Single().GetProperty("id").GetGuid();

        (await admin.DeleteAsync($"/api/admin/memes/{meme}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await SlidesAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Slides_include_photo_hunt_photos()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var hunt = await CreateAsync(admin, "PhotoHunt", "С ослом");
        (await admin.PostAsync($"/api/admin/events/{hunt}/start", null)).EnsureSuccessStatusCode();
        var ann = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);
        (await ann.PostAsync($"/api/photo/{hunt}", ImageForm())).EnsureSuccessStatusCode();

        var slide = (await SlidesAsync()).Single();

        slide.GetProperty("text").GetString().Should().Contain("С ослом").And.Contain("Ann");
        slide.GetProperty("logins").EnumerateArray().Select(l => l.GetString()).Should().Equal("Ann");
    }

    [Fact]
    public async Task Slides_include_hide_shots_with_caught_players()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var (admin, hide) = await StartHideShootingAsync();

        (await admin.PostAsync($"/api/admin/hide/{hide}/shots", ShotForm(ann, bob))).EnsureSuccessStatusCode();

        var slide = (await SlidesAsync()).Single();
        slide.GetProperty("text").GetString().Should().Contain("Прятки").And.Contain("Ann").And.Contain("Bob");
        slide.GetProperty("logins").EnumerateArray().Select(l => l.GetString()).Should().BeEquivalentTo(["Ann", "Bob"]);
        slide.GetProperty("imageUrl").GetString().Should().StartWith("/media/");
    }

    [Fact]
    public async Task Slides_include_hide_shot_without_players()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (admin, hide) = await StartHideShootingAsync();

        (await admin.PostAsync($"/api/admin/hide/{hide}/shots", ShotForm())).EnsureSuccessStatusCode();

        var slide = (await SlidesAsync()).Single();
        slide.GetProperty("text").GetString().Should().Contain("Прятки");
        slide.GetProperty("logins").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Reset_keeps_memes_and_captions_but_clears_message()
    {
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsync("/api/admin/memes", ImageForm())).EnsureSuccessStatusCode();
        await PostMessageOkAsync(admin, "Привет", seconds: 600);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/memes"))!.Should().HaveCount(1);
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/captions"))!.Should().HaveCount(5);
        (await SnapshotAsync()).TvMessage.Should().BeNull();
    }

    public void Dispose() => _factory.Dispose();

    private static MultipartFormDataContent ImageForm()
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { file, "file", "pic.jpg" } };
    }

    private static MultipartFormDataContent ShotForm(params Guid[] userIds)
    {
        var form = ImageForm();
        form.Add(new StringContent(string.Join(',', userIds)), "userIds");
        return form;
    }

    private async Task<(HttpClient Admin, Guid Id)> StartHideShootingAsync()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var hide = await CreateAsync(admin, "HideAndSeek", "Прятки");
        (await admin.PostAsync($"/api/admin/events/{hide}/start", null)).EnsureSuccessStatusCode();
        _factory.Time.Advance(TimeSpan.FromMinutes(2));
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
        return (admin, hide);
    }

    private static MultipartFormDataContent Form(string text, int seconds, bool withImage = false)
    {
        var form = withImage ? ImageForm() : new MultipartFormDataContent();
        form.Add(new StringContent(text), "text");
        form.Add(new StringContent(seconds.ToString()), "seconds");
        return form;
    }

    private static async Task PostMessageOkAsync(HttpClient admin, string text, int seconds, bool withImage = false) =>
        (await admin.PostAsync("/api/admin/tv/message", Form(text, seconds, withImage))).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task<AppSnapshot> SnapshotAsync()
    {
        AppSnapshot? snapshot = null;
        await _factory.WithServiceAsync<SnapshotBuilder>(async b => snapshot = await b.BuildAsync(new SnapshotAudience(SignedIn: true)));
        return snapshot!;
    }

    private async Task<List<JsonElement>> SlidesAsync()
    {
        var viewer = await TestClients.ViewerAsync(_factory);
        return (await viewer.GetFromJsonAsync<List<JsonElement>>("/api/tv/slides"))!;
    }

    private static async Task<Guid> CreateAsync(HttpClient admin, string type, string title)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type, title }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }
}
