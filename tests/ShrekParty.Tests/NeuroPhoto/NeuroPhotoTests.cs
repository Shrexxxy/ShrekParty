using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShrekParty.Api.Data;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.NeuroPhoto;

public class NeuroPhotoTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Upload_and_mark_photos()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);

        var photo = await UploadAsync(admin);
        (await admin.PutAsJsonAsync($"/api/admin/neuro/photos/{photo}", new { subjectId = ann })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/neuro/photos"))!;
        list.Single().GetProperty("subjectId").GetGuid().Should().Be(ann);
        list.Single().GetProperty("url").GetString().Should().StartWith("/media/");
    }

    [Fact]
    public async Task Mark_unknown_user_400()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var photo = await UploadAsync(admin);

        (await admin.PutAsJsonAsync($"/api/admin/neuro/photos/{photo}", new { subjectId = Guid.NewGuid() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admin_can_be_on_photo()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetGuid();
        var photo = await UploadAsync(admin);

        (await admin.PutAsJsonAsync($"/api/admin/neuro/photos/{photo}", new { subjectId = adminId })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await StartAsync(admin);

        var options = (await DetailsAsync(admin)).GetProperty("options").EnumerateArray().Select(o => o.GetProperty("userId").GetGuid());
        options.Should().Contain(adminId);
    }

    [Fact]
    public async Task Delete_photo()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var photo = await UploadAsync(admin);

        (await admin.DeleteAsync($"/api/admin/neuro/photos/{photo}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/neuro/photos"))!.Should().BeEmpty();
    }

    [Fact]
    public async Task Start_without_photos_409()
    {
        await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Start_with_unmarked_photo_409()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        await UploadAsync(admin);
        var id = await CreateAsync(admin);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Guess_and_change()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        var id = await StartAsync(admin);
        var player = await PlayerAsync("Bob");

        await GuessOkAsync(player, id, bob);
        await GuessOkAsync(player, id, ann);

        var details = await DetailsAsync(player);
        details.GetProperty("myGuess").GetGuid().Should().Be(ann);
        details.GetProperty("guessed").GetInt32().Should().Be(1);
        details.GetProperty("subjectId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Guess_self_allowed()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        var id = await StartAsync(admin);

        await GuessOkAsync(await PlayerAsync("Ann"), id, ann);
    }

    [Fact]
    public async Task Guess_outside_guessing_409()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        await MarkedPhotoAsync(admin, ann);
        var id = await StartAsync(admin);
        await AdvanceAsync(TimeSpan.FromSeconds(20));

        (await GuessAsync(await PlayerAsync("Ann"), id, ann)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cycle_photos_then_finish()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        await MarkedPhotoAsync(admin, ann);
        var id = await StartAsync(admin);

        var first = await DetailsAsync(admin);
        first.GetProperty("index").GetInt32().Should().Be(0);
        first.GetProperty("total").GetInt32().Should().Be(2);
        first.GetProperty("phase").GetString().Should().Be("Guessing");
        first.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(20));

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Reveal");

        await AdvanceAsync(TimeSpan.FromSeconds(5));
        var second = await DetailsAsync(admin);
        second.GetProperty("index").GetInt32().Should().Be(1);
        second.GetProperty("phase").GetString().Should().Be("Guessing");

        await AdvanceAsync(TimeSpan.FromSeconds(20));
        await AdvanceAsync(TimeSpan.FromSeconds(5));
        (await StatusAsync(id)).Should().Be(EventStatus.Finished);
    }

    [Fact]
    public async Task Reveal_shows_subject_and_correct()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        var id = await StartAsync(admin);
        await GuessOkAsync(await PlayerAsync("Ann"), id, ann);
        await GuessOkAsync(await PlayerAsync("Bob"), id, bob);

        await AdvanceAsync(TimeSpan.FromSeconds(20));

        var details = await DetailsAsync(await PlayerAsync("Bob"));
        details.GetProperty("subjectId").GetGuid().Should().Be(ann);
        details.GetProperty("correct").EnumerateArray().Select(c => c.GetString()).Should().Equal("Ann");
    }

    [Fact]
    public async Task Rank_by_correct_dense()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var bob = await _factory.SeedPlayerAsync("Bob");
        await _factory.SeedPlayerAsync("Cid");
        await _factory.SeedPlayerAsync("Dan");
        var admin = await TestClients.AdminAsync(_factory);
        await MarkedPhotoAsync(admin, ann);
        await MarkedPhotoAsync(admin, bob);
        var id = await StartAsync(admin);
        var (pAnn, pBob, pCid, pDan) = (await PlayerAsync("Ann"), await PlayerAsync("Bob"), await PlayerAsync("Cid"), await PlayerAsync("Dan"));
        await GuessOkAsync(pAnn, id, ann);
        await GuessOkAsync(pBob, id, ann);
        await GuessOkAsync(pCid, id, bob);
        await GuessOkAsync(pDan, id, bob);
        await AdvanceAsync(TimeSpan.FromSeconds(20));
        await AdvanceAsync(TimeSpan.FromSeconds(5));
        await GuessOkAsync(pAnn, id, bob);
        await GuessOkAsync(pBob, id, ann);
        await GuessOkAsync(pCid, id, bob);

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        // Ann — 2 угаданных; Bob и Cid — по 1; Dan — 0, вне рейтинга. Linear, 2 места, максимум 50.
        (await TabAsync(admin, id)).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 50, ["Bob"] = 25, ["Cid"] = 25, ["Dan"] = 0 });
    }

    [Fact]
    public async Task Reset_keeps_photos_clears_player_marks()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        var admin = await TestClients.AdminAsync(_factory);
        var adminId = (await admin.GetFromJsonAsync<JsonElement>("/api/auth/me")).GetProperty("id").GetGuid();
        await MarkedPhotoAsync(admin, ann);
        await MarkedPhotoAsync(admin, adminId);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        var list = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/neuro/photos"))!;
        list.Should().HaveCount(2);
        list.Select(p => p.GetProperty("subjectId").ValueKind == JsonValueKind.Null ? (Guid?)null : p.GetProperty("subjectId").GetGuid())
            .Should().BeEquivalentTo(new Guid?[] { null, adminId });
    }

    public void Dispose() => _factory.Dispose();

    private static async Task<Guid> UploadAsync(HttpClient admin)
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var response = await admin.PostAsync("/api/admin/neuro/photos", new MultipartFormDataContent { { file, "file", "neuro.jpg" } });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task MarkedPhotoAsync(HttpClient admin, Guid subjectId)
    {
        var photo = await UploadAsync(admin);
        (await admin.PutAsJsonAsync($"/api/admin/neuro/photos/{photo}", new { subjectId })).EnsureSuccessStatusCode();
    }

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "NeuroPhoto", title = "Нейрофото" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private static async Task<Guid> StartAsync(HttpClient admin)
    {
        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return id;
    }

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    private Task<HttpClient> PlayerAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static Task<HttpResponseMessage> GuessAsync(HttpClient player, Guid id, Guid userId) =>
        player.PostAsJsonAsync($"/api/neuro/{id}/guess", new { userId });

    private static async Task GuessOkAsync(HttpClient player, Guid id, Guid userId) =>
        (await GuessAsync(player, id, userId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private async Task<EventStatus> StatusAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Events.SingleAsync(e => e.Id == id)).Status;
    }

    private static async Task<Dictionary<string, int>> TabAsync(HttpClient admin, Guid id)
    {
        var rows = await admin.GetFromJsonAsync<List<LeaderboardRowDto>>($"/api/leaderboard?eventId={id}", TestClients.Json);
        return rows!.ToDictionary(r => r.Login, r => r.Donkeys);
    }
}
