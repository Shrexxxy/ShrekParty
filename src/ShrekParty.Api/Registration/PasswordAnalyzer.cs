namespace ShrekParty.Api.Registration;

/// <summary>
/// Оценка пароля.
/// </summary>
/// <param name="Score">Оценка zxcvbn 0–4.</param>
/// <param name="GuessesLog10">log10 числа попыток подбора — точнее, чем Score, для ранжирования.</param>
/// <param name="Patterns">Найденные паттерны (dictionary, spatial, repeat, sequence, regex, date, bruteforce).</param>
public record PasswordAnalysis(int Score, double GuessesLog10, IReadOnlyList<string> Patterns);

/// <summary>
/// Оценивает сложность пароля.
/// </summary>
public interface IPasswordAnalyzer
{
    /// <summary>Оценить пароль.</summary>
    PasswordAnalysis Analyze(string password);
}

/// <summary>
/// Оценка через zxcvbn.
/// </summary>
public class ZxcvbnPasswordAnalyzer : IPasswordAnalyzer
{
    /// <summary>Паттерн «есть в словаре популярных паролей».</summary>
    public const string CommonPasswordPattern = "common_password";

    /// <inheritdoc/>
    public PasswordAnalysis Analyze(string password)
    {
        var result = Zxcvbn.Core.EvaluatePassword(password);
        var patterns = result.MatchSequence.Select(PatternOf).Distinct().ToList();
        return new PasswordAnalysis(result.Score, result.GuessesLog10, patterns);
    }

    /// <summary>Совпадение со словарём популярных паролей выделяем отдельно — это самый смешной факт.</summary>
    private static string PatternOf(Zxcvbn.Matcher.Matches.Match match) =>
        match is Zxcvbn.Matcher.Matches.DictionaryMatch { DictionaryName: "passwords" }
            ? CommonPasswordPattern
            : match.Pattern;
}
