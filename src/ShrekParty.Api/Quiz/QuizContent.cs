namespace ShrekParty.Api.Quiz;

/// <summary>Тип медиа вопроса.</summary>
public enum QuizMediaType
{
    Image,
    Audio,
    Video,
}

/// <summary>
/// Тема Своей игры. Раунд 0 — финал (одна тема с одним вопросом).
/// </summary>
public class QuizTheme
{
    public Guid Id { get; set; }

    public int Round { get; set; }

    public int Order { get; set; }

    public required string Name { get; set; }
}

/// <summary>
/// Вопрос Своей игры; ответ видит только именинник.
/// </summary>
public class QuizQuestion
{
    public Guid Id { get; set; }

    public Guid ThemeId { get; set; }

    public int Order { get; set; }

    /// <summary>Стоимость; у финального вопроса — 0 (там ставки).</summary>
    public int Price { get; set; }

    public required string Text { get; set; }

    public required string Answer { get; set; }

    public Guid? MediaId { get; set; }

    public QuizMediaType? MediaType { get; set; }
}
