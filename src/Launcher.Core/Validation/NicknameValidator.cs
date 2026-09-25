using System.Text.RegularExpressions;

namespace Launcher.Core.Validation;

public static partial class NicknameValidator
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 16;

    public static bool IsValid(string? nickname) =>
        nickname is not null && NicknamePattern().IsMatch(nickname);

    public static string? GetError(string? nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            return "Введите ник.";
        }

        if (nickname.Length is < MinimumLength or > MaximumLength)
        {
            return "Ник должен содержать от 3 до 16 символов.";
        }

        return NicknamePattern().IsMatch(nickname)
            ? null
            : "Используйте только латинские буквы, цифры и знак подчёркивания.";
    }

    [GeneratedRegex("^[A-Za-z0-9_]{3,16}$", RegexOptions.CultureInvariant)]
    private static partial Regex NicknamePattern();
}
