using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.PhotoHunt;

public class PhotoHuntTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_sets_task_and_deadline_from_settings()
    {
        await _factory.SeedPlayerAsync("Ann");

        var (_, admin) = await StartAsync(new { task = "Сфоткайтесь втроём на столе", durationSec = 120 });

        var details = await DetailsAsync(admin);
        details.GetProperty("task").GetString().Should().Be("Сфоткайтесь втроём на столе");
        details.GetProperty("phase").GetString().Should().Be("Shooting");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(120));
    }

    [Fact]
    public async Task Default_is_three_minutes_and_title_as_task()
    {
        await _factory.SeedPlayerAsync("Ann");

        var (_, admin) = await StartAsync(settings: null);

        var details = await DetailsAsync(admin);
        details.GetProperty("task").GetString().Should().Be("Фотоохота");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddMinutes(3));
    }

    [Fact]
    public async Task Upload_shows_my_photo()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(settings: null);
        var ann = await PlayerAsync("Ann");

        await UploadOkAsync(ann, id);

        (await DetailsAsync(ann)).GetProperty("myPhotoUrl").GetString().Should().StartWith("/media/");
    }

    [Fact]
    public async Task Upload_again_replaces_photo()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(settings: null);
        var ann = await PlayerAsync("Ann");
        await UploadOkAsync(ann, id);
        var first = (await DetailsAsync(ann)).GetProperty("myPhotoUrl").GetString();

        await UploadOkAsync(ann, id);

        (await DetailsAsync(ann)).GetProperty("myPhotoUrl").GetString().Should().NotBe(first);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PhotoSubmissions.CountAsync()).Should().Be(1);
        (await db.MediaFiles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Rapid_uploads_keep_one_photo()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(settings: null);
        var ann = await PlayerAsync("Ann");

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => UploadAsync(ann, id)));

        responses.Select(r => r.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.NoContent);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PhotoSubmissions.CountAsync()).Should().Be(1);
        (await db.MediaFiles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Upload_after_deadline_409_even_before_tick()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(settings: null);
        _factory.Time.Advance(TimeSpan.FromMinutes(3));

        var response = await UploadAsync(await PlayerAsync("Ann"), id);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("too_late");
    }

    [Fact]
    public async Task Deadline_moves_to_rating_and_shows_wall_to_everyone()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, _) = await StartAsync(settings: null);
        var ann = await PlayerAsync("Ann");
        var bob = await PlayerAsync("Bob");
        await UploadOkAsync(ann, id);
        (await DetailsAsync(bob)).GetProperty("photos").GetArrayLength().Should().Be(0);

        _factory.Time.Advance(TimeSpan.FromMinutes(3));
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());

        var details = await DetailsAsync(bob);
        details.GetProperty("phase").GetString().Should().Be("Rating");
        details.GetProperty("photos").EnumerateArray().Single().GetProperty("login").GetString().Should().Be("Ann");
    }

    [Fact]
    public async Task Admin_sees_photos_before_deadline()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync(settings: null);

        await UploadOkAsync(await PlayerAsync("Ann"), id);

        (await DetailsAsync(admin)).GetProperty("photos").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Likes_rank_and_award()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        // Dan без фото — в рейтинг не попадает.
        await _factory.SeedPlayerAsync("Dan");
        var (id, admin) = await StartAsync(settings: null);
        var (a, b, c) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"), await PlayerAsync("Cid"));
        await UploadOkAsync(a, id);
        await UploadOkAsync(b, id);
        await UploadOkAsync(c, id);
        await ToRatingAsync();
        var (annId, bobId) = (await IdAsync("Ann"), await IdAsync("Bob"));
        await LikeOkAsync(b, id, annId);
        await LikeOkAsync(c, id, annId);
        await LikeOkAsync(a, id, bobId);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        var tab = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        tab!.Where(r => r.Donkeys > 0).Select(r => (r.Login, r.Donkeys)).Should().Equal(("Ann", 20), ("Bob", 13), ("Cid", 7));
    }

    [Fact]
    public async Task Without_photo_gets_nothing()
    {
        await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var (id, admin) = await StartAsync(settings: null);
        await UploadOkAsync(await PlayerAsync("Ann"), id);
        await ToRatingAsync();

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.DonkeyAwards.AnyAsync(x => x.EventId == id && x.UserId == bob)).Should().BeFalse();
        (await db.DonkeyAwards.AnyAsync(x => x.EventId == id)).Should().BeTrue();
    }

    [Fact]
    public async Task Like_and_unlike_show_count_and_mine()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, _) = await StartAsync(settings: null);
        var (a, b) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"));
        await UploadOkAsync(a, id);
        await ToRatingAsync();
        var annId = await IdAsync("Ann");

        await LikeOkAsync(b, id, annId);
        var liked = (await DetailsAsync(b)).GetProperty("photos")[0];
        liked.GetProperty("likes").GetInt32().Should().Be(1);
        liked.GetProperty("likedByMe").GetBoolean().Should().BeTrue();

        (await b.DeleteAsync($"/api/photo/{id}/likes/{annId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DetailsAsync(b)).GetProperty("photos")[0].GetProperty("likes").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Like_twice_counts_once()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, _) = await StartAsync(settings: null);
        var (a, b) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"));
        await UploadOkAsync(a, id);
        await ToRatingAsync();
        var annId = await IdAsync("Ann");

        await LikeOkAsync(b, id, annId);
        await LikeOkAsync(b, id, annId);

        (await DetailsAsync(b)).GetProperty("photos")[0].GetProperty("likes").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Like_own_photo_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, _) = await StartAsync(settings: null);
        var a = await PlayerAsync("Ann");
        await UploadOkAsync(a, id);
        await ToRatingAsync();

        var response = await a.PutAsync($"/api/photo/{id}/likes/{await IdAsync("Ann")}", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("own_photo");
    }

    [Fact]
    public async Task Like_before_deadline_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, _) = await StartAsync(settings: null);
        await UploadOkAsync(await PlayerAsync("Ann"), id);

        var response = await (await PlayerAsync("Bob")).PutAsync($"/api/photo/{id}/likes/{await IdAsync("Ann")}", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorAsync(response)).Should().Be("not_yet");
    }

    [Fact]
    public async Task Like_without_photo_404()
    {
        await _factory.SeedPlayerAsync("Ann");
        await _factory.SeedPlayerAsync("Bob");
        var (id, _) = await StartAsync(settings: null);
        await ToRatingAsync();

        var response = await (await PlayerAsync("Bob")).PutAsync($"/api/photo/{id}/likes/{await IdAsync("Ann")}", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Admin_cannot_like()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync(settings: null);
        await UploadOkAsync(await PlayerAsync("Ann"), id);
        await ToRatingAsync();

        var response = await admin.PutAsync($"/api/photo/{id}/likes/{await IdAsync("Ann")}", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reset_clears_submissions()
    {
        await _factory.SeedPlayerAsync("Ann");
        var (id, admin) = await StartAsync(settings: null);
        await UploadOkAsync(await PlayerAsync("Ann"), id);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.PhotoSubmissions.AnyAsync()).Should().BeFalse();
        (await db.PhotoLikes.AnyAsync()).Should().BeFalse();
        (await db.MediaFiles.AnyAsync()).Should().BeFalse();
    }

    public void Dispose() => _factory.Dispose();

    private async Task<(Guid Id, HttpClient Admin)> StartAsync(object? settings)
    {
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "PhotoHunt", title = "Фотоохота", settings }, TestClients.Json);
        var id = (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> UploadAsync(HttpClient player, Guid id)
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return player.PostAsync($"/api/photo/{id}", new MultipartFormDataContent { { file, "file", "hunt.jpg" } });
    }

    private static async Task UploadOkAsync(HttpClient player, Guid id) =>
        (await UploadAsync(player, id)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task LikeOkAsync(HttpClient player, Guid id, Guid ownerId) =>
        (await player.PutAsync($"/api/photo/{id}/likes/{ownerId}", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task ToRatingAsync()
    {
        _factory.Time.Advance(TimeSpan.FromMinutes(3));
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private async Task<Guid> IdAsync(string login)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.SingleAsync(u => u.Login == login)).Id;
    }

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static async Task<string?> ErrorAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString();
}
