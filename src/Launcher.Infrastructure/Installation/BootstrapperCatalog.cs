using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Infrastructure.Persistence;

namespace Launcher.Infrastructure.Installation;

internal sealed record BootstrapperFile(string Path, long Size, string Sha256);
internal sealed record BootstrapperCatalog(int SchemaVersion, string PackId, string MainExe, string Version,
    string Channel, string VelopackVersion, string UpdateSha256, BootstrapperFile[] Files)
{
    internal const string FileName = "bootstrapper-catalog.json";
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8,
    };

    internal static BootstrapperCatalog Load(string directory, InstallationProduct product)
    {
        using FileStream file = SafeLeafFile.OpenRead(global::System.IO.Path.Combine(directory, FileName));
        if (file.Length is 0 or > 1048576) throw new InvalidDataException("Недопустимый размер установочного каталога.");
        BootstrapperCatalog catalog = JsonSerializer.Deserialize<BootstrapperCatalog>(file, Options)
            ?? throw new InvalidDataException("Установочный каталог отсутствует.");
        catalog.Validate(product);
        catalog.VerifyFiles(directory);
        return catalog;
    }

    internal void Validate(InstallationProduct product)
    {
        if (SchemaVersion != 1 || PackId != product.PackId || MainExe != product.MainExe ||
            VelopackVersion != "1.2.161" || Channel is not ("dev" or "stable") ||
            string.IsNullOrWhiteSpace(Version) || !HashValid(UpdateSha256) || Files is null || Files.Length is < 3 or > 1024)
            throw new InvalidDataException("Версия или идентичность установочного каталога не поддерживается.");
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (BootstrapperFile entry in Files)
        {
            if (entry is null || !ValidPath(entry.Path) || entry.Size is <= 0 or > 2147483648 ||
                !HashValid(entry.Sha256) || !paths.Add(entry.Path))
                throw new InvalidDataException("Некорректная или повторная запись установочного payload.");
        }
        if (!paths.Contains("vendor/Setup.exe") || !paths.Contains("vendor/full.nupkg") ||
            !paths.Contains("helper/Launcher.InstallHelper.exe"))
            throw new InvalidDataException("Установочный payload неполный.");
    }

    internal static bool ValidPath(string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Length > 240 || path.Contains('\\') ||
            !(path.StartsWith("helper/", StringComparison.Ordinal) || path is "vendor/Setup.exe" or "vendor/full.nupkg")) return false;
        return path.Split('/').All(segment => segment.Length > 0 && segment is not ("." or "..") &&
            segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_') &&
            !segment.EndsWith('.') && !IsReserved(segment.Split('.')[0]));
    }

    private static bool IsReserved(string name) => name.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("PRN", StringComparison.OrdinalIgnoreCase) || name.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
        name.Length == 4 && (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
        name[3] is >= '1' and <= '9';

    private static bool HashValid(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);

    internal void VerifyFiles(string directory)
    {
        foreach (BootstrapperFile entry in Files)
        {
            using FileStream file = SafeLeafFile.OpenRead(global::System.IO.Path.Combine(directory, entry.Path.Replace('/', global::System.IO.Path.DirectorySeparatorChar)));
            if (file.Length != entry.Size || !Convert.ToHexString(SHA256.HashData(file)).Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Повреждён установочный файл: " + entry.Path);
        }
    }

    internal InstallationPayload Payload(string directory)
    {
        BootstrapperFile setup = Files.Single(file => file.Path == "vendor/Setup.exe");
        BootstrapperFile package = Files.Single(file => file.Path == "vendor/full.nupkg");
        return new(global::System.IO.Path.Combine(directory, "vendor", "Setup.exe"), setup.Sha256,
            global::System.IO.Path.Combine(directory, "vendor", "full.nupkg"), package.Sha256, UpdateSha256, Version, Channel);
    }
}
