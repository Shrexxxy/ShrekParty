using AwesomeAssertions;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Tests.Scoring;

public class DenseRankingTests
{
    [Fact]
    public void Rank_ties_are_dense()
    {
        var ranked = DenseRanking.Rank(new[] { 30, 10, 10, 50 }, x => x, descending: true);

        ranked.Select(r => (r.Item, r.Place)).Should().Equal(
            (50, 1), (30, 2), (10, 3), (10, 3));
    }

    [Fact]
    public void Rank_ascending_for_time()
    {
        var times = new[] { TimeSpan.FromSeconds(42), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10) };

        var ranked = DenseRanking.Rank(times, x => x, descending: false);

        ranked.Select(r => (r.Item.TotalSeconds, r.Place)).Should().Equal(
            (10d, 1), (10d, 1), (42d, 2));
    }
}
