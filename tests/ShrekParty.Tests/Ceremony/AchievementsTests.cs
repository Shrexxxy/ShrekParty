using AwesomeAssertions;
using ShrekParty.Api.Ceremony;

namespace ShrekParty.Tests.Ceremony;

public class AchievementsTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void One_per_player_by_priority()
    {
        var result = Achievements.Assign(
            [new AchievementCandidates("Первая", [A, B]), new AchievementCandidates("Вторая", [A, B])],
            [A, B, C]);

        result[A].Should().Be("Первая");
        result[B].Should().Be("Вторая");
    }

    [Fact]
    public void Achievement_without_candidates_is_skipped()
    {
        var result = Achievements.Assign([new AchievementCandidates("Пусто", []), new AchievementCandidates("Есть", [C])], [A, C]);

        result[C].Should().Be("Есть");
        result.Values.Should().NotContain("Пусто");
    }

    [Fact]
    public void Leftover_players_get_default()
    {
        var result = Achievements.Assign([new AchievementCandidates("Единственная", [A])], [A, B, C]);

        result[B].Should().Be(Achievements.Default);
        result[C].Should().Be(Achievements.Default);
    }

    [Fact]
    public void Candidate_outside_players_is_ignored()
    {
        var stranger = Guid.NewGuid();

        var result = Achievements.Assign([new AchievementCandidates("Ачивка", [stranger, B])], [A, B]);

        result[B].Should().Be("Ачивка");
        result.Should().NotContainKey(stranger);
    }

    [Fact]
    public void Achievement_left_unassigned_when_all_candidates_taken()
    {
        var result = Achievements.Assign([new AchievementCandidates("Первая", [A]), new AchievementCandidates("Вторая", [A])], [A, B]);

        result[A].Should().Be("Первая");
        result[B].Should().Be(Achievements.Default);
    }
}
