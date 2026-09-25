# Архитектура Launcher MVP-2

## Проекты и зависимости

`Launcher.Core` — независимое ядро. Здесь находятся `GameProfile`, `LauncherSettings`, bootstrap-модель, правила nickname/RAM/Server URL, безопасные ограничения серверного профиля и контракт запуска игры. Core не знает о WPF, CmlLib.Core и Windows UI.

`Launcher.Infrastructure` зависит только от Core и реализует:

- загрузку bootstrap/profiles через один долгоживущий `HttpClient`;
- JSON parsing и валидацию Server Protocol v1;
- хранение settings в per-user каталоге;
- файловый лог;
- определение физической RAM через Windows API;
- `CmlLibGameLaunchService`, который подготавливает и запускает Minecraft.

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
  → instances/<profile-id>
  → official Minecraft files + Mojang Java
  → exact Fabric Loader
  → CmlLib process builder
  → process output / PID / exit code
```

`MainViewModel` вызывает только Core-контракт `IGameLaunchService` через `GameLaunchCoordinator`. Coordinator атомарно запрещает параллельный запуск. Реализация CmlLib находится целиком в Infrastructure; типы CmlLib не проходят в Core или App.

Общие неизменяемые игровые файлы размещаются в `%LOCALAPPDATA%\MinecraftLauncher\assets`, `libraries`, `versions` и `runtime`. Рабочая директория каждого профиля — `%LOCALAPPDATA%\MinecraftLauncher\instances\<profile-id>`. До построения пути profile id валидируется как ограниченный ASCII identifier, а версии не могут содержать path separators.

Установка и проверка выполняются асинхронно с `CancellationToken`. File/task и byte progress CmlLib преобразуются в Core-модель. После старта `ProcessWrapper` передаёт игровой output существующему logger, а Launcher ждёт завершения и обрабатывает exit code. Подробный сценарий описан в [game-launch.md](game-launch.md).

## Settings и logging

`JsonSettingsStore` хранит JSON в `%LOCALAPPDATA%\MinecraftLauncher\settings.json` и заменяет файл через временный файл. Отсутствующий, пустой, повреждённый или старый частичный JSON восстанавливается безопасными defaults. ViewModel отдельно проверяет сохранённые RAM и profile id относительно текущей машины и ответа сервера.

`FileAppLogger` пишет startup, shutdown, подключения, HTTP/JSON errors и необработанные исключения в `%LOCALAPPDATA%\MinecraftLauncher\logs`. Ошибка самого логгера не завершает приложение. Пользователь видит короткое сообщение, а stack trace остаётся только в логе.

## Следующие интеграции

- Pack updater: загрузка manifest, проверка SHA-256, атомарная синхронизация и Repair поверх сохранённого `ManifestUrl`.
- Self-update: отдельный подписанный release channel; Velopack или другой updater следует оценить тогда, когда появятся installer и release pipeline.

Эти подсистемы не реализованы и не имитируются в MVP-2.
