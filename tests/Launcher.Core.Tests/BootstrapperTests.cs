using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Infrastructure.Installation;

namespace Launcher.Core.Tests;

public sealed class BootstrapperTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string Sid = "S-1-5-21-123";

    private static string Message(string command = "install", string directory = @"D:\Игры с пробелами") =>
        JsonSerializer.Serialize(new { v = 1, id = 1, nonce = Nonce, sid = Sid, command, baseDirectory = directory });

    [Theory]
    [InlineData("inspect")]
    [InlineData("preflight")]
    [InlineData("install")]
    [InlineData("cancel")]
    [InlineData("launch")]
    public void CommandsPreserveUnicodeAndNeverAcceptAnExecutable(string command)
    {
        BootstrapperCommand parsed = BootstrapperProtocol.Parse(Message(command), Nonce, Sid);
        Assert.Equal(command, parsed.Command);
        Assert.Equal(@"D:\Игры с пробелами", parsed.BaseDirectory);
        Assert.ThrowsAny<Exception>(() => BootstrapperProtocol.Parse(Message(command).Replace("\"v\":1", "\"exe\":\"evil.exe\",\"v\":1"), Nonce, Sid));
    }

    [Theory]
    [InlineData("\"v\":1", "\"v\":2")]
    [InlineData("\"id\":1", "\"id\":0")]
    [InlineData("\"id\":1", "\"id\":\"1\"")]
    [InlineData("\"v\":1", "\"v\":1,\"v\":1")]
    [InlineData("install", "unknown")]
    [InlineData("\"v\":1", "\"v\":null")]
    public void InvalidCommandsFailClosed(string source, string replacement) =>
        Assert.ThrowsAny<Exception>(() => BootstrapperProtocol.Parse(Message().Replace(source, replacement), Nonce, Sid));

    [Fact]
    public void ContextIsBoundToBothNonceAndWindowsUser()
    {
        Assert.Throws<InvalidDataException>(() => BootstrapperProtocol.Parse(Message(), "other", Sid));
        Assert.Throws<InvalidDataException>(() => BootstrapperProtocol.Parse(Message(), Nonce, "S-1-5-21-999"));
        Assert.Throws<InvalidDataException>(() => BootstrapperProtocol.Parse(Message(directory: ""), Nonce, Sid));
        Assert.Throws<InvalidDataException>(() => BootstrapperProtocol.Parse(Message(directory: new string('x', 2049)), Nonce, Sid));
    }

    [Fact]
    public async Task FramingPreservesUtf8AndRejectsPartialOrOversizedInput()
    {
        using MemoryStream messages = new(Encoding.UTF8.GetBytes(Message() + "\r\n" + Message("cancel") + "\n"));
        Assert.Equal(Message(), await BootstrapperProtocol.ReadLineAsync(messages, default));
        Assert.Equal(Message("cancel"), await BootstrapperProtocol.ReadLineAsync(messages, default));
        Assert.Null(await BootstrapperProtocol.ReadLineAsync(messages, default));
        using MemoryStream partial = new(Encoding.UTF8.GetBytes(Message()));
        await Assert.ThrowsAsync<EndOfStreamException>(() => BootstrapperProtocol.ReadLineAsync(partial, default));
        using MemoryStream large = new(Encoding.UTF8.GetBytes(new string('x', 32769) + "\n"));
        await Assert.ThrowsAsync<InvalidDataException>(() => BootstrapperProtocol.ReadLineAsync(large, default));
        using MemoryStream invalid = new([0xFF, 0x0A]);
        await Assert.ThrowsAsync<DecoderFallbackException>(() => BootstrapperProtocol.ReadLineAsync(invalid, default));
        using MemoryStream cancelled = new([1]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BootstrapperProtocol.ReadLineAsync(cancelled, new(true)));
    }

    [Theory]
    [InlineData("helper/../other.exe")]
    [InlineData("helper/CON.exe")]
    [InlineData("helper/sub/LPT1")]
    [InlineData("helper/a.")]
    [InlineData("helper//file")]
    [InlineData("helper\\file")]
    [InlineData("vendor/evil.exe")]
    [InlineData("/helper/file")]
    [InlineData("helper/file:stream")]
    public void CatalogRejectsUnsafePaths(string path) => Assert.False(BootstrapperCatalog.ValidPath(path));

    [Fact]
    public void CatalogRejectsWrongProductVersionAndDuplicateFiles()
    {
        BootstrapperCatalog valid = Catalog();
        valid.Validate(InstallationProduct.Launcher);
        Assert.Throws<InvalidDataException>(() => valid.Validate(InstallationProduct.Diagnostic));
        Assert.Throws<InvalidDataException>(() => (valid with { SchemaVersion = 2 }).Validate(InstallationProduct.Launcher));
        Assert.Throws<InvalidDataException>(() => (valid with { VelopackVersion = "1.2.158" }).Validate(InstallationProduct.Launcher));
        Assert.Throws<InvalidDataException>(() => (valid with { UpdateSha256 = "fake" }).Validate(InstallationProduct.Launcher));
        Assert.Throws<InvalidDataException>(() => (valid with { Files = [.. valid.Files, valid.Files[0]] }).Validate(InstallationProduct.Launcher));
        Assert.Throws<InvalidDataException>(() => (valid with { Files = valid.Files.Select(f => f with { Size = 0 }).ToArray() }).Validate(InstallationProduct.Launcher));
    }

    [Fact]
    public void ExtractedCatalogPinsEveryFileAndRejectsUnknownMetadata()
    {
        string root = Path.Combine(Path.GetTempPath(), "bootstrapper-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            BootstrapperCatalog catalog = Catalog();
            foreach (BootstrapperFile file in catalog.Files)
            {
                string path = Path.Combine(root, file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, [42]);
            }
            string manifest = Path.Combine(root, BootstrapperCatalog.FileName);
            File.WriteAllText(manifest, JsonSerializer.Serialize(catalog, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            BootstrapperCatalog loaded = BootstrapperCatalog.Load(root, InstallationProduct.Launcher);
            Assert.Equal(Path.Combine(root, "vendor", "Setup.exe"), loaded.Payload(root).SetupFile);
            File.WriteAllBytes(Path.Combine(root, "vendor", "full.nupkg"), [43]);
            Assert.Throws<InvalidDataException>(() => loaded.VerifyFiles(root));
            File.WriteAllText(manifest, "{\"unknown\":true}");
            Assert.Throws<JsonException>(() => BootstrapperCatalog.Load(root, InstallationProduct.Launcher));
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void CatalogDoesNotFollowLeafSymlinkOrModifyExternalData()
    {
        string root = Path.Combine(Path.GetTempPath(), "bootstrapper-link-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "helper"));
        string external = Path.Combine(root, "world.sentinel");
        File.WriteAllBytes(external, [42]);
        try
        {
            File.CreateSymbolicLink(Path.Combine(root, "helper", "Launcher.InstallHelper.exe"), external);
            Assert.ThrowsAny<Exception>(() => Catalog().VerifyFiles(root));
            Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(external));
        }
        finally { Directory.Delete(root, true); }
    }

    private static BootstrapperCatalog Catalog()
    {
        string hash = Convert.ToHexString(SHA256.HashData(new byte[] { 42 }));
        return new(1, InstallationProduct.Launcher.PackId, InstallationProduct.Launcher.MainExe, "0.6.1", "dev", "1.2.161", hash,
            [new("helper/Launcher.InstallHelper.exe", 1, hash), new("vendor/Setup.exe", 1, hash), new("vendor/full.nupkg", 1, hash)]);
    }
}
