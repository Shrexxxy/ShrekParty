namespace ShrekParty.Api.Neuro;

/// <summary>
/// Нейрофото — контент, загруженный заранее; кто на нём, отмечают после регистрации гостей.
/// </summary>
public class NeuroPhoto
{
    public Guid Id { get; set; }

    public Guid MediaId { get; set; }

    /// <summary>Кто на фото: игрок или именинник; <c>null</c> — ещё не отмечено.</summary>
    public Guid? SubjectId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Догадка игрока по одному фото.
/// </summary>
public class NeuroGuess
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid PhotoId { get; set; }

    public Guid UserId { get; set; }

    public Guid GuessId { get; set; }
}
