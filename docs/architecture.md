# Архитектура Launcher MVP-4

## Проекты и зависимости

`Launcher.Core` — независимое ядро. Здесь находятся `GameProfile`, `LauncherSettings`, pack-модели, правила nickname/RAM/Server URL, контракты запуска и `IPackSyncService`. Core не знает о `HttpClient`, JSON DTO, filesystem implementation, WPF, CmlLib.Core и Windows UI.

`Launcher.Infrastructure` зависит только от Core и реализует:

- загрузку bootstrap/profiles через один долгоживущий `HttpClient` с отдельным 30-секундным budget для каждого metadata request;
- JSON parsing и валидацию Server Protocol v1;
- хранение settings в per-user каталоге;
- файловый лог;
- определение физической RAM через Windows API;
- `CmlLibGameLaunchService`, который подготавливает и запускает Minecraft.
- `PackSyncService`, который получает Pack Manifest v1, проверяет реальные files/SHA-256, выполняет staging, atomic publish и managed-state cleanup.
- `VelopackLauncherUpdateService`, который проверяет официальный GitHub Releases source, загружает и передаёт update Velopack для применения.

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

`PackSyncService` получает manifest и pack files через отдельный `HttpClient` с `AllowAutoRedirect = false`. Redirects выполняются вручную; каждый target проверяется до request. Bootstrap, profiles и manifest имеют 30-секундный metadata timeout с отдельным дружелюбным сообщением; cancellation вызывающего кода остаётся cancellation. Этот короткий budget не применяется к потоковой загрузке pack files: она ограничивается caller cancellation и проверками size/SHA-256. Существующие manifest-файлы всегда хешируются, затем вычисляется линейный план downloads и obsolete paths через `HashSet` с `OrdinalIgnoreCase`.

Downloads идут последовательно и потоково в `instances/<profile-id>/.launcher/staging/<operation-id>`. После exact size/SHA-256 verification применяется same-volume `File.Move` или `File.Replace`. Obsolete cleanup касается только путей предыдущего valid `managed-state.json`; unmanaged files не перечисляются и не удаляются. State записывается временным файлом с flush и атомарной заменой только после успешного sync. Подробный контракт: [pack-manifest.md](pack-manifest.md).

Filesystem boundary состоит из строгой portable-path validation, `Path.GetFullPath` containment и проверки существующих parent components на `FileAttributes.ReparsePoint` непосредственно перед hash/write/replace/delete.

## Три независимых lifecycle

```text
Launcher Application Update      Game Runtime Preparation       Pack Synchronization
Velopack/GitHub Releases         CmlLib.Core/Mojang/Fabric      Server Pack Manifest v1
application install root        assets/libraries/runtime       instances/<profile>/mods|config
```

Launcher Application Update меняет только установленное приложение под `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher`. Game Runtime Preparation управляет Minecraft, Fabric, Java, assets и libraries. Pack Synchronization управляет только заявленными сервером файлами сборки в profile instance. Эти механизмы не объединены и не передают друг другу источники или команды.

`ILauncherUpdateService` и собственные Core-модели не содержат типов Velopack. Framework startup hook находится в explicit `Program.Main` и вызывается до WPF startup. После показа `MainWindow` Launcher неблокирующе запускает проверку обновления: отсутствие update и сетевой сбой не меняют основной UI, а найденная версия показывается ненавязчивым banner с действиями «Обновить» и «Позже». Ручная проверка в Settings остаётся доступной и показывает явный результат. Параллельные startup/manual checks разделяют один запрос; download/apply блокируются, пока активны Play, Pack Sync или Repair. Download не начинается автоматически, а apply/restart происходит только по явной команде пользователя.

Production source жёстко принадлежит приложению: `https://github.com/lDiestrol/minecraft-launcher`, без PAT/token и произвольного URL. Installed channel берётся из Velopack package metadata. Ошибка source не блокирует startup или игровые операции.

## Граница доверия update

Игровой сервер управляет `GameProfile`, версиями Minecraft/Fabric, адресом сервера, PackVersion, Manifest и managed `mods`/`config`.

Игровой сервер не управляет Launcher executable, Velopack source, GitHub repository, update channel, installer, application commands, JVM hooks или environment variables. Соответствующих полей нет в bootstrap/profile/manifest моделях.

## Settings и logging

`JsonSettingsStore` хранит JSON в `%LOCALAPPDATA%\MinecraftLauncher\settings.json` и заменяет файл через временный файл. Отсутствующий, пустой, повреждённый или старый частичный JSON восстанавливается безопасными defaults. ViewModel отдельно проверяет сохранённые RAM и profile id относительно текущей машины и ответа сервера.

`FileAppLogger` пишет startup, shutdown, подключения, HTTP/JSON errors и необработанные исключения в `%LOCALAPPDATA%\MinecraftLauncher\logs`. Ошибка самого логгера не завершает приложение. Пользователь видит короткое сообщение, а stack trace остаётся только в логе.

## Release packaging

Authoritative version хранится в `Directory.Build.props`. `scripts/build-release.ps1` выполняет self-contained `win-x64` publish и pinned `vpk 1.2.158`, создаёт Setup/full/portable/release index и `SHA256SUMS.txt`. Детали: [releasing.md](releasing.md).
