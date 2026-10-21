namespace ShrekParty.Api.Scoring;

/// <summary>
/// Элемент рейтинга с занятым местом.
/// </summary>
/// <param name="Item">Ранжируемый элемент.</param>
/// <param name="Place">Место, начиная с 1.</param>
public record Ranked<T>(T Item, int Place);

/// <summary>
/// Плотное ранжирование: равные значения делят место, следующее место идёт без пропуска (1, 1, 2).
/// </summary>
public static class DenseRanking
{
    /// <summary>
    /// Ранжирует элементы по ключу. Результат упорядочен по месту.
    /// </summary>
    /// <param name="items">Элементы.</param>
    /// <param name="key">Ключ сравнения.</param>
    /// <param name="descending"><c>true</c> — больший ключ выше (очки), <c>false</c> — меньший выше (время).</param>
    public static IReadOnlyList<Ranked<T>> Rank<T, TKey>(IEnumerable<T> items, Func<T, TKey> key, bool descending)
    {
        var sorted = descending ? items.OrderByDescending(key) : items.OrderBy(key);
        var comparer = EqualityComparer<TKey>.Default;
        var result = new List<Ranked<T>>();
        var place = 0;
        TKey? previous = default;

        foreach (var item in sorted)
        {
            var current = key(item);
            if (place == 0 || !comparer.Equals(current, previous))
            {
                place++;
                previous = current;
            }

            result.Add(new Ranked<T>(item, place));
        }

        return result;
    }
}
