using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using ShrekParty.Api.Donkeys;
using ShrekParty.Api.Events;
using ShrekParty.Tests.Infrastructure;

namespace ShrekParty.Tests.Auction;

public class AuctionTests : IDisposable
{
    private readonly AppFactory _factory = new();

    [Fact]
    public async Task Start_needs_lots()
    {
        await PlayerAsync("Ann", 100);
        var admin = await TestClients.AdminAsync(_factory);
        var id = await CreateAsync(admin);

        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Edit_during_auction_409()
    {
        await PlayerAsync("Ann", 100);
        var (_, admin) = await StartAsync(("Кружка", 50));

        (await admin.PostAsJsonAsync("/api/admin/auction/lots", new { name = "Ещё", startPrice = 10 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Next_starts_bidding_30s()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartAsync(("Кружка", 50), ("Уши", 30));
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Idle");

        await NextAsync(admin, id);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Bidding");
        details.GetProperty("lot").GetProperty("name").GetString().Should().Be("Кружка");
        details.GetProperty("price").ValueKind.Should().Be(JsonValueKind.Null);
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(30));
        details.GetProperty("left").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task First_bid_takes_start_price()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));

        await BidOkAsync(await LoginAsync("Ann"), id, step: 25);

        var details = await DetailsAsync(admin);
        details.GetProperty("price").GetInt32().Should().Be(50);
        details.GetProperty("leader").GetProperty("login").GetString().Should().Be("Ann");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(15));
    }

    [Fact]
    public async Task Steps_raise_price_and_reset_timer()
    {
        await PlayerAsync("Ann", 100);
        await PlayerAsync("Bob", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        _factory.Time.Advance(TimeSpan.FromSeconds(10));

        await BidOkAsync(await LoginAsync("Bob"), id, 10);

        var details = await DetailsAsync(admin);
        details.GetProperty("price").GetInt32().Should().Be(60);
        details.GetProperty("leader").GetProperty("login").GetString().Should().Be("Bob");
        details.GetProperty("deadline").GetDateTimeOffset().Should().Be(_factory.Time.GetUtcNow().AddSeconds(15));
    }

    [Fact]
    public async Task Bad_step_400()
    {
        await PlayerAsync("Ann", 100);
        await PlayerAsync("Bob", 100);
        var (id, _) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);

        (await BidAsync(await LoginAsync("Bob"), id, 7)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Late_start_bid_409_price_changed()
    {
        await PlayerAsync("Ann", 100);
        await PlayerAsync("Bob", 100);
        var (id, _) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);

        // Bob жал «Беру за 50» одновременно с Ann и не успел.
        var response = await BidAsync(await LoginAsync("Bob"), id, 0, expected: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString().Should().Be("price_changed");
    }

    [Fact]
    public async Task Step_on_stale_price_409_not_charged_more()
    {
        await PlayerAsync("Ann", 100);
        await PlayerAsync("Bob", 100);
        await PlayerAsync("Cid", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        await BidOkAsync(await LoginAsync("Bob"), id, 5, expected: 50);

        // Cid видел цену 50 и жал «+5 → 55», но цена уже 55.
        (await BidAsync(await LoginAsync("Cid"), id, 5, expected: 50)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailsAsync(admin)).GetProperty("price").GetInt32().Should().Be(55);
    }

    [Fact]
    public async Task Leader_cannot_outbid_self()
    {
        await PlayerAsync("Ann", 100);
        var (id, _) = await StartBiddingAsync(("Кружка", 50));
        var ann = await LoginAsync("Ann");
        await BidOkAsync(ann, id, 0);

        (await BidAsync(ann, id, 5)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Bid_over_balance_409()
    {
        await PlayerAsync("Ann", 40);
        var (id, _) = await StartBiddingAsync(("Кружка", 50));

        (await BidAsync(await LoginAsync("Ann"), id, 0)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Admin_cannot_bid()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));

        (await BidAsync(admin, id, 0)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Bid_after_deadline_409()
    {
        await PlayerAsync("Ann", 100);
        var (id, _) = await StartBiddingAsync(("Кружка", 50));
        _factory.Time.Advance(TimeSpan.FromSeconds(30));

        (await BidAsync(await LoginAsync("Ann"), id, 0)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Deadline_with_leader_sells_and_charges()
    {
        await PlayerAsync("Ann", 100);
        await PlayerAsync("Bob", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        await BidOkAsync(await LoginAsync("Bob"), id, 25);

        await AdvanceAsync(TimeSpan.FromSeconds(15));

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Sold");
        details.GetProperty("last").GetProperty("login").GetString().Should().Be("Bob");
        details.GetProperty("last").GetProperty("price").GetInt32().Should().Be(75);
        Balances(details).Should().BeEquivalentTo(new Dictionary<string, int> { ["Ann"] = 100, ["Bob"] = 25 });
        (await DetailsAsync(await LoginAsync("Bob"))).GetProperty("myBalance").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task Balance_counts_previous_purchases()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 80), ("Уши", 30));
        var ann = await LoginAsync("Ann");
        await BidOkAsync(ann, id, 0);
        await AdvanceAsync(TimeSpan.FromSeconds(15));
        await NextAsync(admin, id);

        (await BidAsync(ann, id, 0)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Unsold_goes_to_end_then_dropped()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50), ("Уши", 30));

        await AdvanceAsync(TimeSpan.FromSeconds(30));
        var unsold = await DetailsAsync(admin);
        unsold.GetProperty("phase").GetString().Should().Be("Unsold");
        unsold.GetProperty("left").GetInt32().Should().Be(2);

        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("lot").GetProperty("name").GetString().Should().Be("Уши");
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("lot").GetProperty("name").GetString().Should().Be("Кружка");
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("lot").GetProperty("name").GetString().Should().Be("Уши");
        await AdvanceAsync(TimeSpan.FromSeconds(30));

        // Оба лота дважды без ставок — сняты, очередь пуста.
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Done");
    }

    [Fact]
    public async Task Undo_returns_donkeys_and_lot()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50), ("Уши", 30));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        await AdvanceAsync(TimeSpan.FromSeconds(15));

        (await admin.PostAsync($"/api/admin/auction/{id}/undo", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Idle");
        Balances(details)["Ann"].Should().Be(100);
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("lot").GetProperty("name").GetString().Should().Be("Кружка");
    }

    [Fact]
    public async Task Undo_only_after_sale_409()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));

        (await admin.PostAsync($"/api/admin/auction/{id}/undo", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Done_when_queue_empty_and_no_donkeys_awarded()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        await AdvanceAsync(TimeSpan.FromSeconds(15));
        await NextAsync(admin, id);
        (await DetailsAsync(admin)).GetProperty("phase").GetString().Should().Be("Done");

        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();

        var board = (await admin.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json))!;
        board.Single().Donkeys.Should().Be(100);
    }

    [Fact]
    public async Task Sold_lot_cannot_be_deleted()
    {
        await PlayerAsync("Ann", 100);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        await AdvanceAsync(TimeSpan.FromSeconds(15));
        (await admin.PostAsync($"/api/admin/events/{id}/finish", null)).EnsureSuccessStatusCode();
        var lot = (await admin.GetFromJsonAsync<List<JsonElement>>("/api/admin/auction/lots"))!.Single().GetProperty("id").GetGuid();

        (await admin.DeleteAsync($"/api/admin/auction/lots/{lot}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Fine_during_bidding_leaves_lot_unsold()
    {
        var ann = await _factory.SeedPlayerAsync("Ann");
        await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(ann, 50, DonkeyReasons.StartCapital));
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);

        // Штраф от именинника, пока Ann лидирует: денег на лот уже не хватает.
        await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(ann, -20, "Штраф"));
        await AdvanceAsync(TimeSpan.FromSeconds(15));

        var details = await DetailsAsync(admin);
        details.GetProperty("phase").GetString().Should().Be("Unsold");
        Balances(details)["Ann"].Should().Be(30);
    }

    [Fact]
    public async Task Leaderboard_shows_cash_after_purchase()
    {
        await PlayerAsync("Ann", 100);
        await PlayerAsync("Bob", 90);
        var (id, admin) = await StartBiddingAsync(("Кружка", 50));
        await BidOkAsync(await LoginAsync("Ann"), id, 0);
        await AdvanceAsync(TimeSpan.FromSeconds(15));

        var board = (await admin.GetFromJsonAsync<List<LeaderboardRowDto>>("/api/leaderboard", TestClients.Json))!;

        // Места — по осликам, как до аукциона; «на руках» — минус покупки.
        board.Select(r => (r.Login, r.Donkeys, r.Cash, r.Place)).Should().Equal(("Ann", 100, 50, 1), ("Bob", 90, 90, 2));
    }

    public void Dispose() => _factory.Dispose();

    private async Task PlayerAsync(string login, int donkeys)
    {
        var id = await _factory.SeedPlayerAsync(login);
        await _factory.WithServiceAsync<DonkeyService>(s => s.AwardAsync(id, donkeys, DonkeyReasons.StartCapital));
    }

    private Task<HttpClient> LoginAsync(string login) => TestClients.LoggedInAsync(_factory, login, TestData.PlayerPassword);

    private static async Task<Guid> CreateAsync(HttpClient admin)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/events", new { type = "Auction", title = "Аукцион" }, TestClients.Json);
        return (await created.Content.ReadFromJsonAsync<ProgramItemDto>(TestClients.Json))!.Id;
    }

    private async Task<(Guid Id, HttpClient Admin)> StartAsync(params (string Name, int Price)[] lots)
    {
        var admin = await TestClients.AdminAsync(_factory);
        foreach (var (name, price) in lots)
        {
            await AuctionLotsTests.LotAsync(admin, name, price);
        }

        var id = await CreateAsync(admin);
        (await admin.PostAsync($"/api/admin/events/{id}/start", null)).EnsureSuccessStatusCode();
        return (id, admin);
    }

    private async Task<(Guid Id, HttpClient Admin)> StartBiddingAsync(params (string Name, int Price)[] lots)
    {
        var (id, admin) = await StartAsync(lots);
        await NextAsync(admin, id);
        return (id, admin);
    }

    private static async Task NextAsync(HttpClient admin, Guid id) =>
        (await admin.PostAsync($"/api/admin/auction/{id}/next", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private async Task AdvanceAsync(TimeSpan by)
    {
        _factory.Time.Advance(by);
        await _factory.WithServiceAsync<EventEngine>(e => e.TickAsync());
    }

    /// <summary>Ставка; expected — цена, которую видел игрок (не задана — без проверки).</summary>
    private static Task<HttpResponseMessage> BidAsync(HttpClient client, Guid id, int step, int? expected = null) =>
        client.PostAsJsonAsync($"/api/auction/{id}/bid", new { step, expected });

    private static async Task BidOkAsync(HttpClient client, Guid id, int step, int? expected = null) =>
        (await BidAsync(client, id, step, expected)).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<JsonElement> DetailsAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/events/active")).GetProperty("details");

    private static Dictionary<string, int> Balances(JsonElement details) =>
        details.GetProperty("balances").EnumerateArray().ToDictionary(b => b.GetProperty("login").GetString()!, b => b.GetProperty("balance").GetInt32());
}
