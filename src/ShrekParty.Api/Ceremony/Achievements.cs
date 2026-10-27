namespace ShrekParty.Api.Ceremony;

/// <summary>
/// Кандидаты на ачивку — от лучшего к худшему.
/// </summary>
public record AchievementCandidates(string Title, IReadOnlyList<Guid> Ranked);

/// <summary>
/// Раздача ачивок на церемонии: по приоритету, каждому не больше одной, остальным — «Просто хороший огр».
/// </summary>
public static class Achievements
{
    /// <summary>Ачивка тем, кому ничего не досталось.</summary>
    public const string Default = "Просто хороший огр";

    /// <summary>
    /// Ачивка каждому игроку. Ачивку получает лучший кандидат, у которого ещё нет ачивки.
    /// </summary>
    /// <param name="byPriority">Ачивки в порядке важности.</param>
    /// <param name="players">Все, кого показываем на церемонии.</param>
    public static IReadOnlyDictionary<Guid, string> Assign(IReadOnlyList<AchievementCandidates> byPriority, IReadOnlyList<Guid> players)
    {
        var result = new Dictionary<Guid, string>();
        var known = players.ToHashSet();
        foreach (var achievement in byPriority)
        {
            var winner = achievement.Ranked.FirstOrDefault(p => known.Contains(p) && !result.ContainsKey(p));
            if (winner != Guid.Empty)
            {
                result[winner] = achievement.Title;
            }
        }

        foreach (var player in players.Where(p => !result.ContainsKey(p)))
        {
            result[player] = Default;
        }

        return result;
    }
}
