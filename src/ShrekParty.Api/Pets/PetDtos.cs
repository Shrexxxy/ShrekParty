namespace ShrekParty.Api.Pets;

/// <summary>Питомец с уровнем и характеристиками на сейчас.</summary>
/// <param name="Earned">Заработано за вечер — это опыт.</param>
/// <param name="NextLevelAt">Сколько всего надо заработать до следующего уровня.</param>
public record PetDto(
    Guid UserId,
    string Login,
    string Name,
    PetClass Class,
    bool Hatched,
    int Level,
    int Earned,
    int NextLevelAt,
    int Strength,
    int Agility,
    int Intellect,
    int Health,
    int Damage,
    int Mana);

/// <summary>Питомцы для ТВ и ведущего.</summary>
/// <param name="Pets">Все питомцы — только ведущему, ТВ — пусто.</param>
/// <param name="BeforeFirstEvent">Ни один ивент ещё не стартовал — ТВ показывает счётчик вылупившихся только до него.</param>
public record PetsOverviewDto(int Hatched, int Total, IReadOnlyList<PetDto> Pets, bool BeforeFirstEvent);

/// <summary>Новая кличка.</summary>
public record PetNameRequest(string? Name);
