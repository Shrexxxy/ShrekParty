namespace ShrekParty.Api.Scoring;

/// <summary>
/// Переводит место в количество осликов по схеме ивента.
/// </summary>
public static class PointsCalculator
{
    private static readonly double[] PodiumShares = [1.0, 0.7, 0.5];

    private const double PodiumTailShare = 0.4;

    /// <summary>
    /// Ослики за место.
    /// </summary>
    /// <param name="scheme">Схема.</param>
    /// <param name="place">Место, начиная с 1.</param>
    /// <param name="distinctPlaces">Число различных мест в рейтинге (N).</param>
    /// <param name="maxDonkeys">Максимум осликов за ивент.</param>
    /// <param name="customTable">Таблица для <see cref="ScoringScheme.Custom"/>.</param>
    public static int ForPlace(
        ScoringScheme scheme,
        int place,
        int distinctPlaces,
        int maxDonkeys,
        IReadOnlyDictionary<int, int>? customTable = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(place, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(distinctPlaces, place);

        return scheme switch
        {
            ScoringScheme.Linear => Round(maxDonkeys * (double)(distinctPlaces - place + 1) / distinctPlaces),
            ScoringScheme.Podium => Podium(place, distinctPlaces, maxDonkeys),
            ScoringScheme.Custom => customTable?.GetValueOrDefault(place) ?? 0,
            _ => throw new ArgumentOutOfRangeException(nameof(scheme), scheme, null),
        };
    }

    private static int Podium(int place, int distinctPlaces, int maxDonkeys)
    {
        if (place <= PodiumShares.Length)
        {
            return Round(maxDonkeys * PodiumShares[place - 1]);
        }

        var tailLength = distinctPlaces - PodiumShares.Length;
        return Round(maxDonkeys * PodiumTailShare * (distinctPlaces - place + 1) / tailLength);
    }

    private static int Round(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
