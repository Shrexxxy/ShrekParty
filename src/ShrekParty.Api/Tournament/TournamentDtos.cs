namespace ShrekParty.Api.Tournament;

/// <summary>
/// Решение победителя финала про «Брось вызов имениннику».
/// </summary>
public enum ChallengeState
{
    /// <summary>Финал ещё не сыгран.</summary>
    None,

    /// <summary>Финал сыгран, победитель решает.</summary>
    Offered,

    Declined,

    Accepted,
}

/// <summary>Состояние турнира в Event.StateJson.</summary>
public record TournamentState(ChallengeState? Decision = null);

/// <summary>Игрок в кружке сетки; именинник — с мигающей рамкой.</summary>
public record TournamentPlayerDto(Guid Id, string Login, string? AvatarUrl, bool IsAdmin);

/// <summary>Ставка зрителя на матч.</summary>
public record MyBetDto(Guid OnPlayerId, int Amount);

/// <summary>Матч для экранов; банк и коэффициенты тотализатора — живые.</summary>
/// <param name="NextIndex">Куда проходит победитель (Index матча) — для линий сетки; null — дальше некуда.</param>
public record MatchDto(
    Guid Id,
    int Index,
    TournamentRound Round,
    int RoundIndex,
    TournamentPlayerDto? PlayerA,
    TournamentPlayerDto? PlayerB,
    bool IsBye,
    MatchStatus Status,
    Guid? WinnerId,
    string? Score,
    int Photos,
    int Pool,
    double? OddsA,
    double? OddsB,
    MyBetDto? MyBet,
    int? NextIndex);

/// <summary>Предложение вызова.</summary>
public record ChallengeDto(ChallengeState State, Guid? OfferedToId, bool OfferedToMe);

/// <summary>Детали турнира в снапшоте.</summary>
/// <param name="MyNextMatchId">Ближайший несыгранный матч зрителя — «Ты играешь следующим!».</param>
/// <param name="BetMatchId">Матч, на который сейчас открыт тотализатор; null — ставить не на что.</param>
public record TournamentDetails(string Game, IReadOnlyList<MatchDto> Matches, Guid? MyNextMatchId, ChallengeDto Challenge, Guid? BetMatchId);

/// <summary>Результат матча.</summary>
public record MatchWinnerRequest(Guid WinnerId, string? Score);

/// <summary>Решение про вызов.</summary>
public record ChallengeRequest(bool Accept);
