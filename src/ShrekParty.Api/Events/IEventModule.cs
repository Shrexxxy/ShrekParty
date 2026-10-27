using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Events;

/// <summary>
/// Логика конкретного типа ивента. Общие старт/финиш/начисление делает <see cref="EventEngine"/>.
/// </summary>
public interface IEventModule
{
    /// <summary>Тип ивента, который обслуживает модуль.</summary>
    EventType Type { get; }

    /// <summary>Подготовка при старте (сетка, фазы, дедлайны). Сохранение — на движке.</summary>
    Task StartAsync(Event ev);

    /// <summary>Детали для снапшота — свои для каждого зрителя.</summary>
    /// <param name="viewerId">Игрок, которому шлём; null — ТВ или админ.</param>
    Task<object?> GetDetailsAsync(Event ev, Guid? viewerId, bool isAdmin);

    /// <summary>Переходы по дедлайнам; <c>true</c> — ивент пора завершить.</summary>
    Task<bool> TickAsync(Event ev, DateTimeOffset now);

    /// <summary>Итоговые места при завершении; не всё доиграно — <see cref="EventRuleException"/>.</summary>
    Task<IReadOnlyList<Ranked<Guid>>> RankAsync(Event ev);

    /// <summary>После начисления по схеме — поправки модуля (множитель «вызова имениннику»).</summary>
    Task AfterFinishAsync(Event ev) => Task.CompletedTask;

    /// <summary>
    /// Рассылать снапшот каждый тик, даже без изменений состояния: частые действия (тапы) сами не рассылают,
    /// а экранам нужен живой счёт.
    /// </summary>
    bool BroadcastEveryTick(Event ev) => false;
}
