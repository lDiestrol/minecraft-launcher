# Архитектура Launcher MVP-3

## Проекты и зависимости

`Launcher.Core` — независимое ядро. Здесь находятся `GameProfile`, `LauncherSettings`, pack-модели, правила nickname/RAM/Server URL, контракты запуска и `IPackSyncService`. Core не знает о `HttpClient`, JSON DTO, filesystem implementation, WPF, CmlLib.Core и Windows UI.

`Launcher.Infrastructure` зависит только от Core и реализует:

- загрузку bootstrap/profiles через один долгоживущий `HttpClient`;
- JSON parsing и валидацию Server Protocol v1;
- хранение settings в per-user каталоге;
- файловый лог;
- определение физической RAM через Windows API;
- `CmlLibGameLaunchService`, который подготавливает и запускает Minecraft.
- `PackSyncService`, который получает Pack Manifest v1, проверяет реальные files/SHA-256, выполняет staging, atomic publish и managed-state cleanup.

`Launcher.App` зависит от Core и Infrastructure. Это WPF presentation layer, ViewModels, команды и composition root в `App.xaml.cs`. `MainWindow.xaml.cs` содержит только `InitializeComponent()`.

```text
Launcher.App ──────────────> Launcher.Core
      │                           ▲
      └──> Launcher.Infrastructure┘
```

Service Locator не используется. Объекты явно создаются composition root и передаются во ViewModel через конструктор.

## Onboarding и подключение

При отсутствии сохранённого URL показывается onboarding. После ввода URL ViewModel:

1. валидирует и нормализует его в Core;
2. получает bootstrap;
3. проверяет `schemaVersion` и обязательные поля;
4. разрешает `profilesUrl` через `System.Uri`;
5. получает и полностью валидирует profiles;
6. сохраняет новый сервер только после полного успеха;
7. переключает UI на основной экран.

Смена сервера проходит тем же путём. Неудачная попытка не уничтожает предыдущие рабочие настройки. Состояния UI — ожидание, получение конфигурации, получение профилей, готово и ошибка; во время HTTP используется indeterminate progress.

## Game launch flow

```text
server profile
  → validated GameLaunchRequest
  → fetch/validate Pack Manifest
  → hash/plan/stage/verify/publish managed files
  → instances/<profile-id>
  → official Minecraft files + Mojang Java
  → exact Fabric Loader
  → CmlLib process builder
  → process output / PID / exit code
```

`MainViewModel` вызывает `LauncherOperationCoordinator`. Один атомарный guard охватывает Play, Repair, pack sync и время жизни Minecraft process. Play сначала вызывает `IPackSyncService`; `IGameLaunchService` не вызывается при pack-ошибке. Repair выполняет только sync. Реализации PackSync и CmlLib находятся целиком в Infrastructure; их типы не проходят в Core или App.

Общие неизменяемые игровые файлы размещаются в `%LOCALAPPDATA%\MinecraftLauncher\assets`, `libraries`, `versions` и `runtime`. Рабочая директория каждого профиля — `%LOCALAPPDATA%\MinecraftLauncher\instances\<profile-id>`. До построения пути profile id валидируется как ограниченный ASCII identifier, а версии не могут содержать path separators.

Установка и проверка выполняются на worker thread с `CancellationToken`, поэтому синхронные участки CmlLib не занимают WPF dispatcher. File/task и byte progress CmlLib преобразуются в Core-модель и возвращаются в UI через `Progress<T>`. После старта `ProcessWrapper` передаёт игровой output существующему logger, а Launcher ждёт завершения, обрабатывает exit code и освобождает process handle. Подробный сценарий описан в [game-launch.md](game-launch.md).

## Pack sync и managed state

`PackSyncService` получает manifest и pack files через отдельный `HttpClient` с `AllowAutoRedirect = false`. Redirects выполняются вручную; каждый target проверяется до request. Существующие manifest-файлы всегда хешируются, затем вычисляется линейный план downloads и obsolete paths через `HashSet` с `OrdinalIgnoreCase`.

Downloads идут последовательно и потоково в `instances/<profile-id>/.launcher/staging/<operation-id>`. После exact size/SHA-256 verification применяется same-volume `File.Move` или `File.Replace`. Obsolete cleanup касается только путей предыдущего valid `managed-state.json`; unmanaged files не перечисляются и не удаляются. State записывается временным файлом с flush и атомарной заменой только после успешного sync. Подробный контракт: [pack-manifest.md](pack-manifest.md).

Filesystem boundary состоит из строгой portable-path validation, `Path.GetFullPath` containment и проверки существующих parent components на `FileAttributes.ReparsePoint` непосредственно перед hash/write/replace/delete.

## Settings и logging

`JsonSettingsStore` хранит JSON в `%LOCALAPPDATA%\MinecraftLauncher\settings.json` и заменяет файл через временный файл. Отсутствующий, пустой, повреждённый или старый частичный JSON восстанавливается безопасными defaults. ViewModel отдельно проверяет сохранённые RAM и profile id относительно текущей машины и ответа сервера.

`FileAppLogger` пишет startup, shutdown, подключения, HTTP/JSON errors и необработанные исключения в `%LOCALAPPDATA%\MinecraftLauncher\logs`. Ошибка самого логгера не завершает приложение. Пользователь видит короткое сообщение, а stack trace остаётся только в логе.

## Следующие интеграции

- Self-update: отдельный подписанный release channel; Velopack или другой updater следует оценить тогда, когда появятся installer и release pipeline.

Installer, release packaging и self-update не реализованы и не имитируются в MVP-3.
