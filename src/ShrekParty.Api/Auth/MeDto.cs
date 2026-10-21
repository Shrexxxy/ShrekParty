using ShrekParty.Api.Data.Entities;

namespace ShrekParty.Api.Auth;

/// <summary>
/// Текущий пользователь.
/// </summary>
public record MeDto(Guid Id, string Login, UserRole Role, string? AvatarUrl);

/// <summary>
/// Запрос входа.
/// </summary>
public record LoginRequest(string Login, string Password);
