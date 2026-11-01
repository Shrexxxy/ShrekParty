using AwesomeAssertions;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Tests.Scoring;

public class PointsCalculatorTests
{
    [Fact]
    public void Linear_nine_players_max_100()
    {
        var points = Enumerable.Range(1, 9)
            .Select(place => PointsCalculator.ForPlace(place, distinctPlaces: 9, maxDonkeys: 100));

        points.Should().Equal(100, 89, 78, 67, 56, 44, 33, 22, 11);
    }

    [Fact]
    public void Linear_two_places_max_10()
    {
        PointsCalculator.ForPlace(1, distinctPlaces: 2, maxDonkeys: 10).Should().Be(10);
        PointsCalculator.ForPlace(2, distinctPlaces: 2, maxDonkeys: 10).Should().Be(5);
    }
}
