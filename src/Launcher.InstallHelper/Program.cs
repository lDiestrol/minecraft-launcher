using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Launcher.Infrastructure.Installation;
using Launcher.Infrastructure.Persistence;
using Microsoft.Win32.SafeHandles;

namespace Launcher.InstallHelper;

internal static class Program
{
    private static readonly object OutputLock = new();
    private static async Task<int> Main(string[] args)
    {
        Console.InputEncoding = new UTF8Encoding(false, true);
        Console.OutputEncoding = new UTF8Encoding(false);
        string nonce = "", sid = "";
        long responseId = 1;
        CancellationTokenSource operationCancellation = new();
        Task operation = Task.CompletedTask;
        try
        {
#if INSTALLER_DIAGNOSTIC
            const int argumentCount = 5;
#else
            const int argumentCount = 4;
#endif
            if (args.Length != argumentCount || !Guid.TryParseExact(args[0], "N", out _) ||
                !int.TryParse(args[1], out int parentId) || !long.TryParse(args[2], out long parentStart))
                throw new InvalidDataException("Помощник должен запускаться графическим установщиком.");
            nonce = args[0]; sid = args[3];
            using Process parent = Process.GetProcessById(parentId);
            ValidateIdentity(parent, parentStart, sid);
            InheritedPayloadPins.RevokeFurtherInheritance(Environment.GetEnvironmentVariable(InheritedPayloadPins.EnvironmentKey));
            Environment.SetEnvironmentVariable(InheritedPayloadPins.EnvironmentKey, null);
            string payloadDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
#if INSTALLER_DIAGNOSTIC
            if (!Guid.TryParseExact(args[4], "N", out _)) throw new InvalidDataException("Неверный идентификатор диагностического запуска.");
            InstallationProduct product = InstallationProduct.Diagnostic;
            string localData = Path.Combine(Path.GetDirectoryName(parent.MainModule!.FileName)!, "diagnostic-profile");
#else
            InstallationProduct product = InstallationProduct.Launcher;
            string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#endif
            BootstrapperCatalog catalog = BootstrapperCatalog.Load(payloadDirectory, product);
            WindowsInstallationBackend backend = new(localData, AppContext.BaseDirectory,
                () => { ValidateIdentity(parent, parentStart, sid); return WindowsInstallationBackend.CurrentUserSid(); },
                (bytes, volume) => new DriveInfo(volume).AvailableFreeSpace >= bytes, true, product: product);
            InstallationCoordinator coordinator = new(backend);
            InstallationRequest? completedRequest = null;
            long lastId = 0;
            using Stream input = Console.OpenStandardInput();
            while (await BootstrapperProtocol.ReadLineAsync(input, default).ConfigureAwait(false) is string line)
            {
                BootstrapperCommand command = BootstrapperProtocol.Parse(line, nonce, sid);
                if (command.Id <= lastId) throw new InvalidDataException("Повторное установочное сообщение.");
                lastId = command.Id;
                ValidateIdentity(parent, parentStart, sid);
                if (command.Command == "cancel") { operationCancellation.Cancel(); continue; }
                if (!operation.IsCompleted && Volatile.Read(ref completedRequest) is null) { Write(command.Id, "failure", ("error", "Busy"), ("message", "Установка уже выполняется.")); continue; }
                await operation.ConfigureAwait(false);
                responseId = command.Id;
                if (command.Command == "inspect")
                {
                    InstallationInspection inspection = coordinator.Inspect();
                    Write(command.Id, "inspection", ("game", inspection.ConfiguredGameData ??
                        (inspection.HasLegacyData ? new StorageRootResolver(localData, AppContext.BaseDirectory).LegacyRootDirectory : "")),
                        ("existing", inspection.HasLegacyApp), ("pending", inspection.HasPendingInstallation));
                }
                else if (command.Command == "launch")
                {
                    if (completedRequest is null) throw new InvalidDataException("Успешная установка ещё не подтверждена.");
                    string executable;
                    FileStream pinnedExecutable;
                    using (IDisposable lease = backend.AcquireLease())
                    {
                        InstallationPlan plan = backend.Preflight(completedRequest);
                        if (!backend.IsVerified(plan)) throw new InstallationException(InstallationError.Verification, "Установленное приложение больше не проходит проверку.");
                        executable = Path.Combine(plan.AppDirectory, "current", product.MainExe);
                        pinnedExecutable = SafeLeafFile.OpenRead(executable);
                    }
                    // Release the exclusive lease BEFORE Launcher enters its startup guard.
                    ProcessStartInfo start = new(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable)! };
#if INSTALLER_DIAGNOSTIC
                    start.ArgumentList.Add("--bootstrapper-launch-id");
                    start.ArgumentList.Add(args[4]);
#endif
                    using (pinnedExecutable)
                    using (Process? launched = Process.Start(start))
                    {
                        if (launched is null) throw new IOException("Не удалось запустить установленное приложение.");
#if INSTALLER_DIAGNOSTIC
                        Write(command.Id, "launched", ("launchId", args[4]), ("pid", launched.Id), ("startUtcTicks", launched.StartTime.ToUniversalTime().Ticks));
#else
                        Write(command.Id, "launched");
#endif
                    }
                }
                else
                {
                    InstallationRequest request = new(command.BaseDirectory!, sid, catalog.Payload(payloadDirectory));
                    if (command.Command == "preflight")
                    {
                        InstallationPreflightResult plan = coordinator.Preflight(request);
                        Write(command.Id, "preflight", ("app", plan.AppDirectory), ("game", plan.GameDataDirectory),
                            ("requiredBytes", plan.RequiredBytes), ("availableBytes", new DriveInfo(Path.GetPathRoot(plan.AppDirectory)!).AvailableFreeSpace));
                    }
                    else
                    {
                        completedRequest = null;
                        operationCancellation.Dispose();
                        operationCancellation = new();
                        CancellationToken token = operationCancellation.Token;
                        operation = Task.Run(async () =>
                        {
                            try
                            {
                                catalog.VerifyFiles(payloadDirectory);
                                InlineProgress progress = new(value => Write(command.Id, "progress",
                                    ("stage", value.Stage.ToString()), ("message", value.Message)));
                                InstallationResult result = await coordinator.InstallAsync(request, progress, token).ConfigureAwait(false);
                                Volatile.Write(ref completedRequest, request);
                                Write(command.Id, "success", ("app", result.AppDirectory), ("game", result.GameDataDirectory),
                                    ("alreadyInstalled", result.AlreadyInstalled));
                            }
                            catch (Exception exception) { Failure(command.Id, exception); }
                        });
                    }
                }
            }
            operationCancellation.Cancel(); // EOF / GUI crash is cancellation, not completion.
            await operation.ConfigureAwait(false);
            return 0;
        }
        catch (Exception exception)
        {
            operationCancellation.Cancel();
            await operation.ConfigureAwait(false);
            Failure(responseId, exception);
            return 1;
        }
        finally { operationCancellation.Dispose(); }

        void Write(long id, string type, params (string Name, object? Value)[] fields)
        {
            Dictionary<string, object?> envelope = new() { ["v"] = 1, ["id"] = id, ["nonce"] = nonce, ["sid"] = sid, ["type"] = type };
            foreach ((string name, object? value) in fields) envelope.Add(name, value);
            lock (OutputLock) { Console.Out.WriteLine(JsonSerializer.Serialize(envelope)); Console.Out.Flush(); }
        }
        void Failure(long id, Exception exception)
        {
            string error = exception switch
            {
                InstallationException installation => installation.Error.ToString(),
                StorageRootException storage => storage.Error.ToString(),
                OperationCanceledException => "Cancelled",
                UnauthorizedAccessException => "AccessDenied",
                JsonException or InvalidDataException or FormatException or KeyNotFoundException or InvalidOperationException or DecoderFallbackException => "Protocol",
                _ => "Unavailable",
            };
            // Diagnostic details never share stdout with unstructured logging.
            Console.Error.WriteLine(exception.ToString());
            Write(id, "failure", ("error", error), ("message", exception is InstallationException or StorageRootException
                ? exception.Message : error == "Cancelled" ? "Установка отменена. Игровые данные сохранены; можно повторить попытку." :
                "Установку завершить не удалось. Данные сохранены. Проверьте диск, права и целостность установщика."));
        }
    }

    private static void ValidateIdentity(Process parent, long start, string sid)
    {
        using Process current = Process.GetCurrentProcess();
        if (WindowsInstallationBackend.CurrentUserSid() != sid || parent.HasExited ||
            parent.StartTime.ToUniversalTime().Ticks != start || parent.SessionId != current.SessionId)
            throw new InstallationException(InstallationError.Identity, "Неверный пользователь или контекст графического установщика.");
        if (!OpenProcessToken(parent.Handle, 8, out SafeFileHandle token)) throw new Win32Exception(Marshal.GetLastWin32Error());
        using (token)
        using (WindowsIdentity identity = new(token.DangerousGetHandle()))
            if (identity.User?.Value != sid) throw new InstallationException(InstallationError.Identity, "Пользователи GUI и помощника не совпадают.");
    }

    private sealed class InlineProgress(Action<InstallationProgress> callback) : IProgress<InstallationProgress>
    {
        public void Report(InstallationProgress value) => callback(value);
    }
    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeFileHandle token);
}
