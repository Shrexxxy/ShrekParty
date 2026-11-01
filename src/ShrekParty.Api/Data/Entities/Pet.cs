using ShrekParty.Api.Pets;

namespace ShrekParty.Api.Data.Entities;

/// <summary>
/// Драконоослик игрока. Хранится только то, с чем вылупился: уровень и текущие характеристики считаются из заработанных осликов.
/// </summary>
public class Pet
{
    /// <summary>Хозяин — у каждого игрока не больше одного питомца.</summary>
    public Guid UserId { get; set; }

    /// <summary>Кличка.</summary>
    public required string Name { get; set; }

    /// <summary>Класс — по ведущей характеристике.</summary>
    public PetClass Class { get; set; }

    /// <summary>Сила при вылуплении.</summary>
    public int Strength { get; set; }

    /// <summary>Ловкость при вылуплении.</summary>
    public int Agility { get; set; }

    /// <summary>Интеллект при вылуплении.</summary>
    public int Intellect { get; set; }

    /// <summary>Какая неведущая характеристика получает очко прокачки первой.</summary>
    public PetStat FirstMinor { get; set; }

    /// <summary>Когда появилось яйцо.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Когда игрок натапал яйцо; null — ещё яйцо.</summary>
    public DateTimeOffset? HatchedAt { get; set; }
}
