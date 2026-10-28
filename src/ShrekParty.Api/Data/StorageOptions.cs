namespace ShrekParty.Api.Data;

/// <summary>
/// Где лежат данные приложения (база и медиа).
/// </summary>
public class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Папка данных; внутри — <c>shrek.db</c> и <c>media/</c>.</summary>
    public string DataDir { get; set; } = "data";

    /// <summary>Путь к файлу базы.</summary>
    public string DatabasePath => Path.Combine(DataDir, "shrek.db");

    /// <summary>Ключи шифрования cookie — рядом с базой, чтобы вход переживал передеплой контейнера.</summary>
    public string KeysDir => Path.Combine(DataDir, "keys");
}
