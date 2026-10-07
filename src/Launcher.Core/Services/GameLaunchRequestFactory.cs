using Launcher.Core.Models;
using Launcher.Core.Policies;
using Launcher.Core.Validation;

namespace Launcher.Core.Services;

public static class GameLaunchRequestFactory
{
    public static GameLaunchRequest Create(GameProfile profile, string nickname, int ramMb)
    {
        if (!ProfileValueValidator.IsValidProfileId(profile.Id) ||
            !ProfileValueValidator.IsValidVersion(profile.MinecraftVersion) ||
            !ProfileValueValidator.IsValidVersion(profile.LoaderVersion) ||
            !ProfileValueValidator.IsValidServerAddress(profile.ServerAddress))
        {
            throw new GameLaunchException(
                GameLaunchError.InvalidProfile,
                "Профиль сервера содержит недопустимые значения.",
                $"Profile '{profile.Id}' failed game launch validation.");
        }

        if (!profile.LoaderType.Equals("fabric", StringComparison.OrdinalIgnoreCase))
        {
            throw new GameLaunchException(
                GameLaunchError.UnsupportedLoader,
                $"Эта версия Launcher пока не поддерживает загрузчик {profile.LoaderType}.",
                $"Unsupported loader type '{profile.LoaderType}'.");
        }

        if (!NicknameValidator.IsValid(nickname))
        {
            throw new GameLaunchException(
                GameLaunchError.InvalidProfile,
                NicknameValidator.GetError(nickname) ?? "Некорректный ник.",
                "Nickname validation failed before game launch.");
        }

        if (ramMb < RamPolicy.MinimumRamMb)
        {
            throw new GameLaunchException(
                GameLaunchError.InvalidProfile,
                $"Для запуска требуется не менее {RamPolicy.MinimumRamMb / 1024} GB RAM.",
                $"Invalid launch RAM value: {ramMb} MB.");
        }

        return new GameLaunchRequest(
            profile.Id,
            profile.MinecraftVersion,
            profile.LoaderType,
            profile.LoaderVersion,
            nickname,
            ramMb,
            profile.ServerAddress,
            profile.ServerPort);
    }
}
