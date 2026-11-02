using System.Text.Json;

namespace ShrekParty.Api.Neuro;

/// <summary>
/// Раунд нейрофото: друг, замаскированный под персонажа с картинки из интернета. Картинки лежат в
/// <c>web/public/assets/neuro</c> под случайным id — по адресу ответ не прочитать.
/// </summary>
/// <param name="Id">Имя файлов без суффикса.</param>
/// <param name="Answer">Кто на фото — одно из <see cref="NeuroCatalog.Names"/>.</param>
/// <param name="Character">Под кого замаскирован — по-русски, без названия игры или фильма.</param>
public record NeuroRound(string Id, string Answer, string Character)
{
    /// <summary>Что получилось — показываем всем, пока угадывают.</summary>
    public string ResultUrl => $"/assets/neuro/{Id}-result.jpg";

    /// <summary>Фото друга — на раскрытии.</summary>
    public string FriendUrl => $"/assets/neuro/{Id}-friend.jpg";

    /// <summary>Картинка-образ — на раскрытии.</summary>
    public string SourceUrl => $"/assets/neuro/{Id}-source.jpg";
}

/// <summary>
/// Заготовленные раунды нейрофото и варианты ответа. Контент готовится заранее, админка его не трогает.
/// </summary>
public class NeuroCatalog
{
    /// <summary>Люди на выбор — в этом порядке на кнопках. Дима — именинник: не играет, но бывает на фото.</summary>
    public static readonly IReadOnlyList<string> Names =
        ["Дима", "Алеся", "Жека", "Лиза", "Леха", "Кристина", "Саня", "Артем", "Настя", "Соня", "Катя", "Паша"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <param name="rounds">Раунды.</param>
    /// <param name="decoys">Персонажи-обманки: их нет ни в одном раунде, но они есть среди вариантов.</param>
    public NeuroCatalog(IReadOnlyList<NeuroRound> rounds, IReadOnlyList<string> decoys)
    {
        if (rounds.FirstOrDefault(r => !Names.Contains(r.Answer)) is { } bad)
        {
            throw new InvalidOperationException($"Нейрофото {bad.Id}: ответа «{bad.Answer}» нет среди вариантов");
        }

        Rounds = rounds;
        // По алфавиту: порядок не должен подсказывать, кто из раундов, а кто обманка.
        Characters = rounds.Select(r => r.Character).Concat(decoys).Distinct().Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>Все раунды в порядке файла; на старте игры перемешиваются.</summary>
    public IReadOnlyList<NeuroRound> Rounds { get; }

    /// <summary>Персонажи на выбор: из раундов и обманки.</summary>
    public IReadOnlyList<string> Characters { get; }

    /// <summary>Каталог из JSON-файла <c>{ "decoys": [...], "rounds": [{ "id", "answer", "character" }] }</c>.</summary>
    public static NeuroCatalog Load(string path)
    {
        var file = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), Json) ?? new CatalogFile([], []);
        return new NeuroCatalog(file.Rounds, file.Decoys);
    }

    private record CatalogFile(IReadOnlyList<string> Decoys, IReadOnlyList<NeuroRound> Rounds);
}
