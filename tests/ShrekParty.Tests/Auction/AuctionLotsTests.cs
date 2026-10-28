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
