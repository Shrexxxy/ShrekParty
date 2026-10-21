namespace ShrekParty.Api.Registration;

/// <summary>
/// Маска пароля для показа на ТВ: видно не больше 75% символов.
/// </summary>
public static class PasswordMask
{
    private const char Hidden = '*';

    /// <summary>
    /// Скрывает не меньше четверти символов, равномерно по длине.
    /// </summary>
    public static string Mask(string password)
    {
        var length = password.Length;
        if (length == 0)
        {
            return password;
        }

        var hidden = Math.Max(1, (int)Math.Ceiling(length * 0.25));
        var chars = password.ToCharArray();
        for (var k = 0; k < hidden; k++)
        {
            chars[(int)Math.Floor((k + 0.5) * length / hidden)] = Hidden;
        }

        return new string(chars);
    }
}
