namespace ShrekParty.Api.Tournament;

/// <summary>
/// Статус матча.
/// </summary>
public enum MatchStatus
{
    /// <summary>Ждёт — на него можно ставить.</summary>
    Pending,

    /// <summary>Идёт — ставки закрыты.</summary>
    Live,

    Done,
}

/// <summary>
/// Матч турнира. Связи «куда проходит» — по индексу матча внутри ивента.
/// </summary>
public class TournamentMatch
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public int Index { get; set; }

    public TournamentRound Round { get; set; }

    public int RoundIndex { get; set; }

    public Guid? PlayerAId { get; set; }

    public Guid? PlayerBId { get; set; }

    /// <summary>Сторона B — именинник (матч-вызов).</summary>
    public bool IsAdminB { get; set; }

    public bool IsBye { get; set; }

    public int? NextIndex { get; set; }

    public MatchSlot? NextSlot { get; set; }

    public int? LoserNextIndex { get; set; }

    public MatchSlot? LoserNextSlot { get; set; }

    public MatchStatus Status { get; set; }

    public Guid? WinnerId { get; set; }

    /// <summary>Необязательный счёт, например «3:1».</summary>
    public string? Score { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    /// <summary>Проигравший — второй из пары; у прохода без игры — нет.</summary>
    public Guid? LoserId => WinnerId is null || IsBye ? null : WinnerId == PlayerAId ? PlayerBId : PlayerAId;
}
