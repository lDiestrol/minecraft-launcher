using System.Text;
using System.Text.Json;

namespace Launcher.Infrastructure.Installation;

internal sealed record BootstrapperCommand(long Id, string Command, string? BaseDirectory);

internal static class BootstrapperProtocol
{
    internal const int MaximumMessageBytes = 32768;
    internal static BootstrapperCommand Parse(string json, string nonce, string sid)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumMessageBytes) throw new InvalidDataException("Установочное сообщение слишком велико.");
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 2 });
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Ожидалось установочное сообщение.");
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (JsonProperty property in root.EnumerateObject())
            if (!keys.Add(property.Name) || property.Name is not ("v" or "id" or "nonce" or "sid" or "command" or "baseDirectory"))
                throw new InvalidDataException("Неизвестное или повторное поле сообщения.");
        if (root.GetProperty("v").GetInt32() != 1 || root.GetProperty("nonce").GetString() != nonce ||
            root.GetProperty("sid").GetString() != sid || !root.GetProperty("id").TryGetInt64(out long id) || id <= 0)
            throw new InvalidDataException("Неверный контекст установочного сообщения.");
        string? command = root.GetProperty("command").GetString();
        string? directory = root.TryGetProperty("baseDirectory", out JsonElement selected) ? selected.GetString() : null;
        if (command is not ("inspect" or "preflight" or "install" or "cancel" or "launch") ||
            command is "preflight" or "install" && string.IsNullOrWhiteSpace(directory) ||
            directory is { Length: > 2048 }) throw new InvalidDataException("Неверная команда установщика.");
        return new(id, command, directory);
    }

    internal static async Task<string?> ReadLineAsync(Stream input, CancellationToken cancellationToken)
    {
        using MemoryStream line = new();
        byte[] single = new byte[1];
        while (await input.ReadAsync(single, cancellationToken).ConfigureAwait(false) != 0)
        {
            if (single[0] == '\n') return new UTF8Encoding(false, true).GetString(line.ToArray()).TrimEnd('\r');
            if (line.Length >= MaximumMessageBytes) throw new InvalidDataException("Установочное сообщение слишком велико.");
            line.WriteByte(single[0]);
        }
        if (line.Length != 0) throw new EndOfStreamException("Прервано установочное сообщение.");
        return null;
    }
}
