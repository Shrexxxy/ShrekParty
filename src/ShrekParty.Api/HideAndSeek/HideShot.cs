namespace ShrekParty.Api.HideAndSeek;

/// <summary>
/// Снимок со вспышкой в прятках.
/// </summary>
public class HideShot
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid MediaId { get; set; }

    public DateTimeOffset TakenAt { get; set; }
}

/// <summary>
/// Игрок, попавший на снимок.
/// </summary>
public class HideShotPlayer
{
    public Guid Id { get; set; }

    public Guid ShotId { get; set; }

    public Guid UserId { get; set; }
}

/// <summary>
/// Голос за самое смешное фото; один на игрока в ивенте.
/// </summary>
public class HideVote
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid VoterId { get; set; }

    public Guid ShotId { get; set; }
}
