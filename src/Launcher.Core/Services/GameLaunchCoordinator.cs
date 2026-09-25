using Launcher.Core.Models;

namespace Launcher.Core.Services;

public sealed class GameLaunchCoordinator
{
    private readonly IGameLaunchService _service;
    private int _isActive;

    public GameLaunchCoordinator(IGameLaunchService service)
    {
        _service = service;
    }

    public bool IsActive => Volatile.Read(ref _isActive) != 0;

    public async Task<GameLaunchResult> LaunchAsync(
        GameLaunchRequest request,
        IProgress<GameLaunchProgress> progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _isActive, 1, 0) != 0)
        {
            throw new GameLaunchException(
                GameLaunchError.AlreadyRunning,
                "Minecraft уже подготавливается или запущен.",
                "A game launch is already active.");
        }

        try
        {
            return await _service.LaunchAsync(request, progress, cancellationToken);
        }
        finally
        {
            Volatile.Write(ref _isActive, 0);
        }
    }
}
