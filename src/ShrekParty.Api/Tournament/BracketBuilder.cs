namespace ShrekParty.Api.Tournament;

/// <summary>
/// Раунд турнира.
/// </summary>
public enum TournamentRound
{
    /// <summary>Отборочный — когда игроков больше 8.</summary>
    Qualifier,

    Quarter,

    Semi,

    /// <summary>Матч за 3-е место — проигравшие полуфиналов.</summary>
    ThirdPlace,

    Final,

    /// <summary>«Брось вызов имениннику» — победитель против админа.</summary>
    Challenge,
}

/// <summary>
/// Сторона матча, куда проходит игрок.
/// </summary>
public enum MatchSlot
{
    A,
    B,
}

/// <summary>
/// Матч в плане сетки. Индексы упорядочены так, что матч всегда идёт после тех, из которых в него проходят.
/// </summary>
/// <param name="IsBye">Проход без игры: PlayerA проходит дальше сразу.</param>
/// <param name="NextIndex">Куда проходит победитель.</param>
/// <param name="LoserNextIndex">Куда идёт проигравший (полуфинал → матч за 3-е).</param>
public record PlannedMatch(
    int Index,
    TournamentRound Round,
    int RoundIndex,
    Guid? PlayerA,
    Guid? PlayerB,
    bool IsBye,
    int? NextIndex,
    MatchSlot? NextSlot,
    int? LoserNextIndex,
    MatchSlot? LoserNextSlot);

/// <summary>
/// Строит олимпийскую сетку: больше 8 игроков — отборочные до сетки на 8; меньше — проходы без игры
/// до ближайшей степени двойки (8, 4 или просто финал на двоих). Есть матч за 3-е место (от 3 игроков).
/// </summary>
public static class BracketBuilder
{
    private const int MainSize = 8;
    private const int MaxPlayers = 16;

    /// <summary>
    /// План сетки для всех игроков в случайном порядке.
    /// </summary>
    public static IReadOnlyList<PlannedMatch> Build(IReadOnlyList<Guid> players, Random random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(players.Count, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(players.Count, MaxPlayers);

        var shuffled = players.ToArray();
        random.Shuffle(shuffled);

        var n = shuffled.Length;
        var size = n > MainSize ? MainSize : NextPowerOfTwo(n);
        var qualifierCount = Math.Max(0, n - MainSize);
        var mainRounds = MainRounds(size);
        var hasThird = size >= 4;

        // Индексы: отборочные, затем раунды сетки по порядку, матч за 3-е перед финалом.
        var roundStart = new Dictionary<TournamentRound, int>();
        var index = qualifierCount;
        foreach (var round in mainRounds.Where(r => r != TournamentRound.Final))
        {
            roundStart[round] = index;
            index += MatchesIn(size, mainRounds, round);
        }

        var thirdIndex = hasThird ? index++ : (int?)null;
        roundStart[TournamentRound.Final] = index;

        var matches = new List<PlannedMatch>();
        var firstRound = mainRounds[0];
        var firstCount = size / 2;

        // Участники первого раунда сетки: прямые игроки, затем победители отборочных; сначала стороны A, потом B.
        var entrants = new List<(Guid? Player, int? Qualifier)>();
        entrants.AddRange(shuffled.Skip(2 * qualifierCount).Select(p => ((Guid?)p, (int?)null)));
        entrants.AddRange(Enumerable.Range(0, qualifierCount).Select(q => ((Guid?)null, (int?)q)));
        var slotOf = new Dictionary<int, (int Match, MatchSlot Slot)>();

        for (var q = 0; q < qualifierCount; q++)
        {
            matches.Add(new PlannedMatch(q, TournamentRound.Qualifier, q, shuffled[2 * q], shuffled[2 * q + 1], false, null, null, null, null));
        }

        for (var j = 0; j < firstCount; j++)
        {
            var a = j < entrants.Count ? entrants[j] : default;
            var b = firstCount + j < entrants.Count ? entrants[firstCount + j] : default;
            if (a.Qualifier is { } qa)
            {
                slotOf[qa] = (roundStart[firstRound] + j, MatchSlot.A);
            }
            if (b.Qualifier is { } qb)
            {
                slotOf[qb] = (roundStart[firstRound] + j, MatchSlot.B);
            }

            var isBye = b.Player is null && b.Qualifier is null;
            matches.Add(Main(roundStart, mainRounds, size, firstRound, j, a.Player, b.Player, isBye, thirdIndex));
        }

        foreach (var round in mainRounds.Skip(1))
        {
            for (var j = 0; j < MatchesIn(size, mainRounds, round); j++)
            {
                matches.Add(Main(roundStart, mainRounds, size, round, j, null, null, false, thirdIndex));
            }
        }

        if (thirdIndex is { } third)
        {
            matches.Add(new PlannedMatch(third, TournamentRound.ThirdPlace, 0, null, null, false, null, null, null, null));
        }

        // Отборочные знают, куда проходят, только после раскладки первого раунда.
        for (var q = 0; q < qualifierCount; q++)
        {
            matches[q] = matches[q] with { NextIndex = slotOf[q].Match, NextSlot = slotOf[q].Slot };
        }

        return matches.OrderBy(m => m.Index).ToList();
    }

    /// <summary>
    /// Первый раунд основной сетки (без отборочных).
    /// </summary>
    public static TournamentRound FirstMainRound(this IReadOnlyList<PlannedMatch> plan) =>
        plan.Where(m => m.Round is TournamentRound.Quarter or TournamentRound.Semi or TournamentRound.Final)
            .Min(m => m.Round);

    private static PlannedMatch Main(
        Dictionary<TournamentRound, int> roundStart,
        IReadOnlyList<TournamentRound> rounds,
        int size,
        TournamentRound round,
        int j,
        Guid? a,
        Guid? b,
        bool isBye,
        int? thirdIndex)
    {
        var position = rounds.ToList().IndexOf(round);
        var hasNext = position + 1 < rounds.Count;
        var next = hasNext ? roundStart[rounds[position + 1]] + j / 2 : (int?)null;
        var nextSlot = hasNext ? (j % 2 == 0 ? MatchSlot.A : MatchSlot.B) : (MatchSlot?)null;
        var isSemi = round == TournamentRound.Semi;
        return new PlannedMatch(
            roundStart[round] + j,
            round,
            j,
            a,
            b,
            isBye,
            next,
            nextSlot,
            isSemi ? thirdIndex : null,
            isSemi ? (j == 0 ? MatchSlot.A : MatchSlot.B) : null);
    }

    private static int MatchesIn(int size, IReadOnlyList<TournamentRound> rounds, TournamentRound round) =>
        size >> (rounds.ToList().IndexOf(round) + 1);

    private static IReadOnlyList<TournamentRound> MainRounds(int size) => size switch
    {
        8 => [TournamentRound.Quarter, TournamentRound.Semi, TournamentRound.Final],
        4 => [TournamentRound.Semi, TournamentRound.Final],
        _ => [TournamentRound.Final],
    };

    private static int NextPowerOfTwo(int n)
    {
        var size = 2;
        while (size < n)
        {
            size *= 2;
        }

        return size;
    }
}
