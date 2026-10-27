using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Tournament;

/// <summary>
/// Итог сыгранного матча.
/// </summary>
/// <param name="Loser">null — проход без игры.</param>
public record MatchResult(TournamentRound Round, Guid? Winner, Guid? Loser);

/// <summary>
/// Места турнира: финал → 1/2, матч за 3-е → 3/4, проигравшие 1/4 делят следующее место,
/// проигравшие отборочных — ещё одно (плотно). Матч-вызов на места не влияет.
/// </summary>
public static class TournamentRanking
{
    /// <summary>
    /// Места по результатам всех матчей.
    /// </summary>
    public static IReadOnlyList<Ranked<Guid>> Rank(IEnumerable<MatchResult> results)
    {
        var list = results.ToList();
        var final = list.Single(r => r.Round == TournamentRound.Final);
        var third = list.SingleOrDefault(r => r.Round == TournamentRound.ThirdPlace);

        // Группы по убыванию: каждая непустая группа — следующее место.
        var groups = new List<IEnumerable<Guid?>>
        {
            new[] { final.Winner },
            new[] { final.Loser },
            new[] { third?.Winner },
            new[] { third?.Loser },
            LosersOf(list, TournamentRound.Quarter),
            LosersOf(list, TournamentRound.Qualifier),
        };

        var ranking = new List<Ranked<Guid>>();
        var place = 0;
        foreach (var group in groups)
        {
            var players = group.OfType<Guid>().ToList();
            if (players.Count == 0)
            {
                continue;
            }

            place++;
            ranking.AddRange(players.Select(p => new Ranked<Guid>(p, place)));
        }

        return ranking;
    }

    private static IEnumerable<Guid?> LosersOf(List<MatchResult> results, TournamentRound round) =>
        results.Where(r => r.Round == round).Select(r => r.Loser);
}
