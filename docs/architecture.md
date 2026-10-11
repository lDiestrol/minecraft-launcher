# Архитектура Launcher v0.6.0

Актуальное состояние, согласованные версии и проверки: [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
Этот документ описывает реализованную архитектуру; планы новых функций отмечены в журнале.

## Проекты и зависимости

**Локальный Storage Root review fix требует повторного независимого review:** intent
v3/witness v1 интегрирован после реальных Java17/21 crash checks. Основная JVM наследует
query-only Job handle; отсутствие имени допускает recovery только по этому versioned
контракту и после доказанного завершения guardian. Произвольные потомки после выхода
основной JVM исключены из lifetime гарантии. Результаты и история остановленных
прототипов — [PROJECT_LEDGER.md](PROJECT_LEDGER.md). Контракт v0.6.0 описан отдельно.

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

`Launcher.App` зависит от Core и Infrastructure. Это WPF presentation layer, ViewModels, команды и composition root в `App.xaml.cs`. `MainWindow.xaml.cs` содержит `InitializeComponent()`, адаптивную компоновку и обработчики WindowChrome (minimize/maximize/close/system menu); бизнес-операции остаются во ViewModel/Core-сервисах.

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

`MainViewModel` вызывает `LauncherOperationCoordinator`. Координатор получает profile-scoped lease через `IProfileOperationLock` для Play, Repair и managed Delete. Один профиль защищён от конфликтующих операций между процессами Launcher, разные профили могут работать одновременно. Счётчик активных операций координирует ограничения app update в текущем процессе. Play сначала вызывает `IPackSyncService`; `IGameLaunchService` не вызывается при pack-ошибке. Repair выполняет только sync. Реализации PackSync и CmlLib находятся в Infrastructure; типы сторонних библиотек не проходят в Core/ViewModel.

Общие неизменяемые игровые файлы размещаются в `<GameData>\assets`, `libraries`, `versions` и `runtime`. Рабочая директория каждого профиля — `<GameData>\instances\<profile-id>`. Default/legacy GameData — `%LOCALAPPDATA%\MinecraftLauncher`; первый локальный программный этап v0.6.1 позволяет сохранить другой root без изменения серверных схем. До построения пути profile id валидируется как ограниченный ASCII identifier, а версии не могут содержать path separators.

### Storage root: локальный этап v0.6.1

`StorageRootResolver` — общая локальная authority для App и guardian. Указатель
`%LOCALAPPDATA%\MinecraftLauncherControl\storage-root.json` содержит только
`schemaVersion: 1` и `gameDataRoot` (абсолютный локальный путь); он не зависит от
версии/расположения App. Control использует per-user LocalApplicationData и его
унаследованные права, без серверных URL/credentials/launch options. JSON ограничен
16 KiB, неизвестные поля/версии и повреждение запрещены. Запись — уникальный временный
файл в Control, flush-to-disk, same-volume atomic rename; игровые файлы не перемещаются.

`OpenSession()` без указателя сохраняет default v0.6.0, включая settings и worlds,
без миграции и без записи нового указателя. С указателем root обязан существовать,
проходить reparse/containment проверки и реальную write probe. Недоступный диск,
повреждение конфигурации или отказ доступа не выбирают другой root. App показывает
понятную startup-ошибку; guardian отказывает до чтения недоверенного request.

Session удерживает shared file lease в Control на весь lifetime App/guardian,
включая ожидание Minecraft; `ConfigureNewRoot()` требует exclusive lease. Профильные
и shared-install locks остаются внутри единого GameData. API предназначен для будущего
установщика: только первоначальный выбор пустого root или явное закрепление legacy root.
Существующие legacy данные, непустой чужой target или смена уже сохранённого root
отклоняются, а не мигрируются. Нет UI/CLI выбора root и установщика в этом этапе.

GameData не допускается внутри App/Control, выше этих каталогов, в корне тома,
UNC/device path или через reparse points (проверяется также цепочка предков).
Для обычного Velopack `current` с соседним `Update.exe` защищается весь install root;
для portable/development — actual AppContext.BaseDirectory, а не предположение о C:.
Все сборки одного пользователя используют один указатель. Settings/logs пока остаются
в GameData, их формат не меняется; новая конфигурация — только небольшой root locator.
Это не полный storage manager и не защита от произвольного вмешательства того же
локального пользователя между filesystem checks (TOCTOU); обрыв диска во время I/O
остаётся ошибкой операции, а не поводом переключиться на C:.

Session lease не является единственной защитой после crash: durable launch intent
в `.locks/<profile>.game.json` записывается до создания Java. WindowsGameJob имеет
уникальное Global имя с user SID/token и protected user/SYSTEM DACL; collision отклоняется.
AtomicJobProcess назначает Job через JOB_LIST непосредственно в CreateProcessW и передаёт
только stdio + query-only witness через HANDLE_LIST. Нет kill-on-close/breakaway/fallback.
Guardian закрывает parent duplicate после creation; основная JVM сохраняет witness до exit.

Recovery сериализован `.game.lock`, root selection — дополнительно exclusive Control lease.
Живой guardian или Job с ActiveProcesses>0 блокирует конфликтующие операции. При доказанно
завершённом guardian пустой Job или отсутствующее имя **v3/witness-v1** позволяют recovery;
при записанном PID его alive/uncertain identity является fail-closed противоречием.
AccessDenied/unknown state не являются отсутствием. Schema1 остаётся PID-based legacy;
schema2 без witness не освобождает missing pre-PID intent. Timeout cleanup отсутствует.
Потомок без witness может пережить main и потерять имя Job; после main exit такой descendant
не входит в гарантированный lifecycle v0.6.1. Это не обещание завершения всего дерева.

Установка и проверка выполняются на worker thread с `CancellationToken`, поэтому синхронные участки CmlLib не занимают WPF dispatcher. File/task и byte progress CmlLib преобразуются в Core-модель и возвращаются в UI через `Progress<T>`. Подготовка общих Minecraft/Fabric/runtime-файлов и передача запуска защищены `ISharedInstallLock`. CmlLib строит `ProcessStartInfo`, а `GameProcessGuardianClient` передаёт запуск отдельному guardian-процессу. Guardian запускает Java, пишет output в logger и удерживает профильную защиту до выхода Minecraft, даже после закрытия Launcher. Launcher наблюдает status/exit code через `GuardedGameSession`. Подробный сценарий описан в [game-launch.md](game-launch.md).

## Pack sync и managed state

`PackSyncService` получает manifest и pack files через отдельный `HttpClient` с `AllowAutoRedirect = false`. Redirects выполняются вручную; каждый target проверяется до request. Bootstrap, profiles и manifest имеют 30-секундный metadata timeout с отдельным дружелюбным сообщением; cancellation вызывающего кода остаётся cancellation. Этот короткий budget не применяется к потоковой загрузке pack files: она ограничивается caller cancellation и проверками size/SHA-256. Существующие manifest-файлы всегда хешируются, затем вычисляется линейный план downloads и obsolete paths через `HashSet` с `OrdinalIgnoreCase`.

Downloads идут последовательно и потоково в `instances/<profile-id>/.launcher/staging/<operation-id>`. После exact size/SHA-256 verification применяется same-volume `File.Move` или `File.Replace`. Obsolete cleanup касается только путей предыдущего valid `managed-state.json`; unmanaged files не перечисляются и не удаляются. State записывается временным файлом с flush и атомарной заменой только после успешного sync. Подробный контракт: [pack-manifest.md](pack-manifest.md).

Атомарность относится к каждому файлу и замене state отдельно. Journal/backup/rollback всей pack-транзакции не реализованы; I/O failure или crash во время apply могут оставить часть новых файлов со старым state. После последней проверки cancellation до apply публикация/cleanup и запись state завершаются без отмены вызывающего кода.

Filesystem boundary состоит из строгой portable-path validation, `Path.GetFullPath` containment и проверки существующих parent components на `FileAttributes.ReparsePoint` непосредственно перед hash/write/replace/delete.

## Три независимых lifecycle

```text
Launcher Application Update      Game Runtime Preparation       Pack Synchronization
Velopack/GitHub Releases         CmlLib.Core/Mojang/Fabric      Server Pack Manifest v1
application install root        assets/libraries/runtime       instances/<profile>/mods|config
```

Launcher Application Update меняет только установленное приложение (default install root — `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher`). Game Runtime Preparation управляет Minecraft, Fabric, Java, assets и libraries внутри определённого локально GameData. Pack Synchronization управляет только заявленными сервером файлами сборки в profile instance. Эти механизмы не объединены и не передают друг другу источники или команды.

`ILauncherUpdateService` и собственные Core-модели не содержат типов Velopack. Framework startup hook находится в explicit `Program.Main` и вызывается до WPF startup. После показа `MainWindow` Launcher неблокирующе запускает проверку обновления: отсутствие update и сетевой сбой не меняют основной UI, а найденная версия показывается ненавязчивым banner с действиями «Обновить» и «Позже». Ручная проверка в Settings остаётся доступной и показывает явный результат. Параллельные startup/manual checks разделяют один запрос; download/apply блокируются, пока активны Play, Pack Sync или Repair. Download не начинается автоматически, а apply/restart происходит только по явной команде пользователя.

Production source жёстко принадлежит приложению: `https://github.com/lDiestrol/minecraft-launcher`, без PAT/token и произвольного URL. Installed channel берётся из Velopack package metadata. Ошибка source не блокирует startup или игровые операции.

## Граница доверия update

Игровой сервер управляет `GameProfile`, версиями Minecraft/Fabric, адресом сервера, PackVersion, Manifest и managed `mods`/`config`.

Игровой сервер не управляет Launcher executable, Velopack source, GitHub repository, update channel, installer, application commands, JVM hooks или environment variables. Соответствующих полей нет в bootstrap/profile/manifest моделях.

## Settings и logging

`JsonSettingsStore` хранит JSON в `<GameData>\settings.json` и заменяет файл через временный файл на том же томе; legacy/default — `%LOCALAPPDATA%\MinecraftLauncher\settings.json`. Отсутствующий, пустой, повреждённый или старый частичный settings JSON восстанавливается безопасными defaults (это не относится к root locator, который fail-closed). ViewModel отдельно проверяет сохранённые RAM и profile id относительно текущей машины и ответа сервера.

`FileAppLogger` пишет startup, shutdown, подключения, HTTP/JSON errors и необработанные исключения в `<GameData>\logs` (legacy/default — `%LOCALAPPDATA%\MinecraftLauncher\logs`). Пользователь видит короткое сообщение, а stack trace остаётся только в логе. При недоступном root startup показывает StorageRootException без попытки создать лог в другом каталоге.

Settings/log leaf открывается с OPEN_REPARSE_POINT и проверкой фактического opened handle;
заранее существующие ссылки, в том числе dangling, не читаются и не перезаписываются.
Settings использует unique CreateNew temp с Flush(true), не фиксированный settings.json.tmp.
Log handle append-only; невозможность безопасной записи не вызывает запись в другом root.

## Release packaging

Authoritative version хранится в `Directory.Build.props`. `scripts/build-release.ps1` выполняет self-contained `win-x64` publish и pinned `vpk 1.2.158`, создаёт Setup/full/portable/release index и `SHA256SUMS.txt`. Детали: [releasing.md](releasing.md).
