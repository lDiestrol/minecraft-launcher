using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Launcher.Core.Validation;

public static partial class ProfileValueValidator
{
    private static readonly HashSet<string> ReservedWindowsDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public const int MaximumProfileIdLength = 64;
    public const int MaximumVersionLength = 64;

    public static bool IsValidProfileId([NotNullWhen(true)] string? value) =>
        value is not null &&
        ProfileIdPattern().IsMatch(value) &&
        !ReservedWindowsDeviceNames.Contains(value);

    public static bool IsValidVersion([NotNullWhen(true)] string? value) =>
        value is not null && VersionPattern().IsMatch(value);

    public static bool IsValidServerAddress([NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= 255 &&
        !value.Any(char.IsWhiteSpace) &&
        !value.Any(char.IsControl);

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant)]
    private static partial Regex ProfileIdPattern();

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._+-]{0,63}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}
