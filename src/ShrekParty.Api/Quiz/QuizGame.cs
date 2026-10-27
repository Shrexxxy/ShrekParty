namespace ShrekParty.Api.Quiz;

/// <summary>
/// Нажатие кнопки на вопрос: время по часам сервера (телефон пересчитал своё) и порядок прихода.
/// </summary>
public class QuizPress
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid QuestionId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset PressedAt { get; set; }

    /// <summary>Порядок прихода — при равном времени выигрывает пришедший раньше.</summary>
    public int Seq { get; set; }
}

/// <summary>
/// Изменение счёта игрока: ответ на вопрос (± цена) или финал (± ставка).
/// </summary>
public class QuizScoreEntry
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public Guid? QuestionId { get; set; }

    public int Delta { get; set; }
}

/// <summary>
/// Финалист: счёт до финала, ставка, ответ и отметка именинника.
/// </summary>
public class QuizFinalAnswer
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public int ScoreBefore { get; set; }

    public int? Bet { get; set; }

    public string? Text { get; set; }

    public bool? Correct { get; set; }
}
