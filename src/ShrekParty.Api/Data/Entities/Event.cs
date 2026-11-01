using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Data.Entities;

/// <summary>
/// Тип ивента.
/// </summary>
public enum EventType
{
    /// <summary>Регистрация-ловушка.</summary>
    Registration,

    /// <summary>Турнир на PS5.</summary>
    Tournament,

    /// <summary>Тотализатор турнира — создаётся вместе с ним, отдельная вкладка таблицы.</summary>
    Tote,

    /// <summary>Смехлыст (Jackbox).</summary>
    Laugh,

    /// <summary>Прятки.</summary>
    HideAndSeek,

    /// <summary>Один запуск фотоохоты.</summary>
    PhotoHunt,

    /// <summary>Церемония награждения, ослики не даёт.</summary>
    Ceremony,

    /// <summary>Реакция Шрека: красный → зелёный, кто быстрее тапнет.</summary>
    Reaction,

    /// <summary>Тапалка с драконом: минута тапов.</summary>
    Tapper,

    /// <summary>Сколько мне дней: угадать возраст именинника в днях.</summary>
    Days,

    /// <summary>Угадай по нейрофото.</summary>
    NeuroPhoto,

    /// <summary>Своя игра.</summary>
    Quiz,

    /// <summary>Аукцион после церемонии, ослики не даёт.</summary>
    Auction,

    /// <summary>Мафия; ведёт именинник.</summary>
    Mafia,

    /// <summary>Битва драконоосликов с боссом.</summary>
    BossFight,
}

/// <summary>
/// Статус ивента.
/// </summary>
public enum EventStatus
{
    Draft,
    Active,
    Finished,
}

/// <summary>
/// Ивент вечеринки — общая часть для всех типов.
/// </summary>
public class Event
{
    public Guid Id { get; set; }

    public EventType Type { get; set; }

    public required string Title { get; set; }

    public EventStatus Status { get; set; }

    /// <summary>Порядок в программе вечера.</summary>
    public int Order { get; set; }

    public int MaxDonkeys { get; set; }

    /// <summary>Настройки типа (задание фотоохоты, игра турнира…), JSON.</summary>
    public string? SettingsJson { get; set; }

    /// <summary>Состояние модуля на время ивента (фаза, дедлайн…), JSON.</summary>
    public string? StateJson { get; set; }

    /// <summary>Итог, который нужен после ивента (победитель голосования, снапшот церемонии…), JSON.</summary>
    public string? ResultJson { get; set; }

    /// <summary>Родительский ивент (тотализатор → турнир).</summary>
    public Guid? ParentId { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }
}
