using System.Text.Json;
using ShrekParty.Api.Data.Entities;
using ShrekParty.Api.Scoring;

namespace ShrekParty.Api.Events;

/// <summary>
/// Новый пункт программы вечера.
/// </summary>
/// <param name="MaxDonkeys">null — значение по умолчанию для типа.</param>
/// <param name="Settings">Настройки типа (задание фотоохоты, босс…).</param>
public record CreateEventRequest(
    EventType Type,
    string Title,
    int? MaxDonkeys = null,
    JsonElement? Settings = null);

/// <summary>
/// Пункт программы вечера.
/// </summary>
public record ProgramItemDto(Guid Id, EventType Type, string Title, EventStatus Status, int Order, int MaxDonkeys);

/// <summary>
/// Перестановка в программе.
/// </summary>
public record ReorderRequest(int Order);

/// <summary>
/// Текущий ивент в снапшоте; <see cref="Details"/> — своя форма у каждого типа.
/// </summary>
public record ActiveEventDto(Guid Id, EventType Type, string Title, DateTimeOffset? StartedAt, object? Details);
