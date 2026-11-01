using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Auction;

public class AuctionLotsTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Create_edit_delete_lots()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var first = await LotAsync(admin, "Кружка Шрека", 50);
        await LotAsync(admin, "Уши огра", 30);

        (await admin.PutAsJsonAsync($"/api/admin/auction/lots/{first}", new { name = "Большая кружка", startPrice = 70 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var lots = await LotsAsync(admin);
        lots.Select(l => (l.GetProperty("name").GetString(), l.GetProperty("startPrice").GetInt32())).Should().Equal(("Большая кружка", 70), ("Уши огра", 30));

        (await admin.DeleteAsync($"/api/admin/auction/lots/{first}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LotsAsync(admin)).Should().ContainSingle();
    }

    [Theory]
    [InlineData(" ", 10)]
    [InlineData("Лот", 0)]
    public async Task Bad_lot_400(string name, int startPrice)
    {
        var admin = await TestClients.AdminAsync(_factory);

        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name, startPrice })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Photo_upload_and_remove()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var lot = await LotAsync(admin, "Кружка", 50);

        (await UploadAsync(admin, lot)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LotsAsync(admin)).Single().GetProperty("photoUrl").GetString().Should().StartWith("/media/");

        (await admin.DeleteAsync($"/api/admin/auction/lots/{lot}/photo")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LotsAsync(admin)).Single().GetProperty("photoUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Reset_keeps_lots()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var lot = await LotAsync(admin, "Кружка", 50);
        await UploadAsync(admin, lot);

        (await admin.PostAsync("/api/admin/reset", null)).EnsureSuccessStatusCode();

        var saved = (await LotsAsync(admin)).Single();
        (await admin.GetAsync(saved.GetProperty("photoUrl").GetString())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Player_cannot_edit_lots()
    {
        await _factory.SeedPlayerAsync("Ann");
        var ann = await TestClients.LoggedInAsync(_factory, "Ann", TestData.PlayerPassword);

        (await ann.GetAsync("/api/admin/auction/lots")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #region Призы боссов

    [Fact]
    public async Task Lot_with_boss_and_medal_saved()
    {
        var admin = await TestClients.AdminAsync(_factory);

        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Вилы", startPrice = 10, boss = "Peasant", medal = "Gold" })).EnsureSuccessStatusCode();

        var lot = (await LotsAsync(admin)).Single();
        lot.GetProperty("boss").GetString().Should().Be("Peasant");
        lot.GetProperty("medal").GetString().Should().Be("Gold");
    }

    [Fact]
    public async Task Second_lot_same_boss_medal_conflicts()
    {
        var admin = await TestClients.AdminAsync(_factory);
        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Вилы", startPrice = 10, boss = "Peasant", medal = "Gold" })).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Грабли", startPrice = 10, boss = "Peasant", medal = "Gold" }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Корона", startPrice = 10, boss = "Farquaad", medal = "Gold" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("Peasant", null)]
    [InlineData(null, "Gold")]
    public async Task Boss_without_medal_bad_request(string? boss, string? medal)
    {
        var admin = await TestClients.AdminAsync(_factory);

        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Вилы", startPrice = 10, boss, medal })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Moving_lot_to_final_frees_medal()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var first = await LotAsync(admin, "Вилы", 10);
        (await admin.PutAsJsonAsync($"/api/admin/auction/lots/{first}/prize", new { boss = "Peasant", medal = "Gold" })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/admin/auction/lots/{first}/prize", new { boss = (string?)null, medal = (string?)null })).EnsureSuccessStatusCode();

        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Грабли", startPrice = 10, boss = "Peasant", medal = "Gold" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await LotsAsync(admin)).First().GetProperty("boss").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Price_edit_keeps_prize()
    {
        var admin = await TestClients.AdminAsync(_factory);
        var created = await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Вилы", startPrice = 10, boss = "Peasant", medal = "Gold" });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await admin.PutAsJsonAsync($"/api/admin/auction/lots/{id}", new { name = "Золотые вилы", startPrice = 30 })).EnsureSuccessStatusCode();

        var lot = (await LotsAsync(admin)).Single();
        lot.GetProperty("name").GetString().Should().Be("Золотые вилы");
        lot.GetProperty("boss").GetString().Should().Be("Peasant");
        lot.GetProperty("medal").GetString().Should().Be("Gold");
    }

    #endregion

    public void Dispose() => _factory.Dispose();

    internal static async Task<Guid> LotAsync(HttpClient admin, string name, int startPrice)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name, startPrice });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient admin, Guid lot)
    {
        var file = new ByteArrayContent([1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return admin.PostAsync($"/api/admin/auction/lots/{lot}/photo", new MultipartFormDataContent { { file, "file", "lot.jpg" } });
    }

    private static async Task<List<JsonElement>> LotsAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/auction/lots"))!;
}
