namespace ShrekParty.Api.Poll;

/// <summary>
/// Голос игрока в голосовании; один на игрока, можно менять до закрытия.
/// </summary>
public class PollVote
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid UserId { get; set; }

    public required string Option { get; set; }

    public DateTimeOffset VotedAt { get; set; }
}
