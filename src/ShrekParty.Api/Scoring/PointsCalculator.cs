namespace ShrekParty.Api.Scoring;

/// <summary>
/// Переводит место в количество осликов. Схема одна — линейная: последнее место тоже получает, шаг между местами одинаковый.
/// </summary>
public static class PointsCalculator
{
    /// <summary>
    /// Ослики за место.
    /// </summary>
    /// <param name="place">Место, начиная с 1.</param>
    /// <param name="distinctPlaces">Число различных мест в рейтинге (N).</param>
    /// <param name="maxDonkeys">Максимум осликов за ивент.</param>
    public static int ForPlace(int place, int distinctPlaces, int maxDonkeys)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(place, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(distinctPlaces, place);

        return (int)Math.Round(maxDonkeys * (double)(distinctPlaces - place + 1) / distinctPlaces, MidpointRounding.AwayFromZero);
    }
}
