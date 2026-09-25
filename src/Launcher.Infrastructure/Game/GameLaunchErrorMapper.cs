using System.Text.Json;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.Infrastructure.Game;

public static class GameLaunchErrorMapper
{
    private const int DiskFullHResult = unchecked((int)0x80070070);
    private const int DiskFullHResultLegacy = unchecked((int)0x80070027);

    public static GameLaunchException Map(Exception exception, GameLaunchStage stage)
    {
        if (exception is HttpRequestException)
        {
            return Create(
                GameLaunchError.NetworkUnavailable,
                "Нет подключения к интернету или сервис загрузки недоступен.",
                exception);
        }

        if (exception is UnauthorizedAccessException)
        {
            return Create(
                GameLaunchError.DirectoryAccessDenied,
                "Нет доступа к игровой директории.",
                exception);
        }

        if (exception is IOException ioException &&
            ioException.HResult is DiskFullHResult or DiskFullHResultLegacy)
        {
            return Create(
                GameLaunchError.InsufficientDiskSpace,
                "Недостаточно свободного места для установки Minecraft.",
                exception);
        }

        if (stage == GameLaunchStage.InstallingFabric)
        {
            return Create(
                GameLaunchError.FabricInstallationFailed,
                "Не удалось установить указанную версию Fabric.",
                exception);
        }

        if (stage == GameLaunchStage.PreparingJava)
        {
            return Create(
                GameLaunchError.JavaPreparationFailed,
                "Не удалось подготовить Java runtime.",
                exception);
        }

        if (stage is GameLaunchStage.PreparingLaunch or GameLaunchStage.StartingMinecraft)
        {
            return Create(
                GameLaunchError.ProcessCreationFailed,
                "Не удалось создать Minecraft process.",
                exception);
        }

        if (exception is JsonException || stage == GameLaunchStage.CheckingMinecraft)
        {
            return Create(
                GameLaunchError.MetadataUnavailable,
                "Не удалось получить Minecraft metadata.",
                exception);
        }

        if (exception is IOException ||
            stage is GameLaunchStage.DownloadingMinecraft or GameLaunchStage.CheckingFiles)
        {
            return Create(
                GameLaunchError.DownloadFailed,
                "Не удалось скачать или проверить файл Minecraft.",
                exception);
        }

        return Create(
            GameLaunchError.Unknown,
            "Не удалось подготовить или запустить Minecraft.",
            exception);
    }

    private static GameLaunchException Create(
        GameLaunchError error,
        string userMessage,
        Exception exception) =>
        new(error, userMessage, $"{exception.GetType().Name}: {exception.Message}", exception);
}
