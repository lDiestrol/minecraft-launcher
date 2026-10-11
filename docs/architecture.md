# Архитектура Launcher v0.6.0

Актуальное состояние, согласованные версии и проверки: [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
Этот документ описывает реализованную архитектуру; планы новых функций отмечены в журнале.

## Проекты и зависимости

**Совместная реализация PR #10/#11:** Server/Singleplayer используют общий pipeline;
Storage Root review завершён до публикации PR #11. Intent v3/witness v1 интегрирован
после реальных Java17/21 crash checks. Основная JVM наследует
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
  → validated GameLaunchRequest (Server / Singleplayer)
  → fetch/validate Pack Manifest
  → hash/plan/stage/verify/publish managed files
  → instances/<profile-id>
  → official Minecraft files + Mojang Java
  → exact Fabric Loader
  → CmlLib process builder
  → atomic guardian Job launch / output / PID / exit code
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

## Installation Coordinator (API v1)

`InstallationCoordinator` в Infrastructure предоставляет managed API для будущего
native Win32 GUI через отдельный managed helper. GUI, IPC transport и helper packaging
ещё не реализованы. `IInstallationBackend` отделяет порядок операций от Windows/FS.
Основные операции: `Inspect()`, `SuggestBaseDirectory(volumeRoot)`, `Preflight(request)`
и `InstallAsync(request, progress, cancellationToken)`. Preflight возвращает фактические
App/GameData/staging paths и RequiredBytes; выполняет короткие CreateNew/DeleteOnClose
write probes, но не создаёт установку/locator. Inspect не создаёт legacy root.

`InstallationRequest` содержит выбранный base, ожидаемый SID и `InstallationPayload`.
Setup/full-package/Update hashes, версия и channel поступают только из доверенного
локального release catalog будущего bootstrapper, не из серверных JSON или произвольного
сетевого ответа. SHA-256 без доверенного catalog не является проверкой издателя.
Progress содержит этап и русское сообщение; `InstallationException.Error` — доменная
категория. StorageRootException, I/O/ACL/Win32 exceptions и cancellation сохраняются;
helper должен преобразовать их в диагностический ответ, не трактовать как успех/fallback.

Чистая установка использует sibling App/GameData и `.launcher-install/<token>` на
выбранном локальном томе. Legacy/configured GameData остаётся прежним, без миграции.
Непустой неизвестный App и unmanaged legacy App отвергаются; in-place обновление
такой v0.6.0 установки в этой API не реализовано. Receipt в Control фиксирует владение
путями, но не является подтверждением успеха: отдельно проверяются layout, HKCU и completion.
Staging и GameData не могут совпадать или содержать друг друга; canonical/reparse проверки
выполняются до intent/locator и повторяются под installation lease.

Coordinator удерживает exclusive `storage-root.lock` на весь critical section и
повторяет preflight под lease. Он использует существующий resolver и game activity checks,
а не второй root locator. Durable `installation-intent.json` schema1/job-contract1
содержит owner PID/start time, уникальный token, App/GameData и package hash. Intent
публикуется до ConfigureStorage/native creation. Пока он существует, новые Launcher/
guardian sessions и ConfigureNewRoot блокируются даже после crash владельца lease.

`WindowsInstallationJob` отличается от игрового Job: защищённое Global имя с SID/token,
atomic JOB_LIST, explicit HANDLE_LIST с query-only witness, **KILL_ON_JOB_CLOSE только
для нового установочного дерева**, без breakaway или launch fallback. Setup/hook могут
пережить crash helper благодаря witness. Recovery под exclusive lease требует завершения
прежнего owner (либо повторной операции того же owner) и отсутствия активных процессов Job.
Missing Job допускает recovery только для проверенного install-contract1 с kill-on-close;
это правило не применяется к Minecraft intent3/witness1. Unknown/corrupt/access-denied
state остаётся fail-closed. Таймаут не разрешает очистку intent. Recovery не удаляет gate:
intent сохраняется до подтверждённого Complete либо атомарно заменяется новой попыткой.
Receipt и pending intent должны точно совпадать по token, owner PID/start time, paths,
package hash и версиям контрактов. Связанная завершённая операция подтверждается completion.
Только при такой связи допускаются соседние backup/чужие элементы base; они не принимаются
во владение, не перемещаются и не удаляются. App.* без связи ничего не разрешает;
reparse siblings отвергаются. Строгая проверка чистой установки сохраняется.

Setup получает `--silent --installto App --log staging/setup.log`, TEMP/TMP на выбранном
томе. Cancellation/15-minute execution budget прекращают только созданный installer Job;
intent/locator сохраняются до проверяемого retry. Нет удаления GameData или rollback.
После exit0 проверяются current files, manifest identity/version/channel/MainExe/x64,
trusted Update hash, установленный full package и configured root. Отдельно проверяется
HKCU uninstall key точного PackId: canonical InstallLocation/DisplayIcon, короткая
major.minor.patch версия Setup161, title/authors из доверенного manifest, команды удаления
точного Update.exe и обязательные поля регистрации. Чужой registered App не заменяется.
Отсутствие необязательных пользовательских shortcuts не считается повреждением.
После успешного native exit0, ActiveProcesses=0 и обеих проверок атомарно записывается
`installation-completion/installation-intent.json` в Control (та же строгая schema1,
но отдельная семантика успешного завершения, не предварительного receipt). Completion
должен совпадать с receipt и текущим pending intent; новая попытка не принимает старый token.
Только эта тройная проверка снимает gate. Crash до completion требует повторного Setup,
даже при полных файлах/HKCU; crash после completion допускает verified retry без Setup.
Unique staging/logs сохраняются для диагностики; автоматической рекурсивной очистки нет.

До VelopackApp.Run `InstallationStartupGuard.TryAcquireVelopackStartup()` выдаёт shared
Control lease, удерживаемый Program.Main до завершения SDK Run, и проверяет pending gate
под lease. Run остаётся непосредственным вызовом в Main, не в async/lambda. Исключение —
только `--veloapp-install` hook внутри указанного installer Job. Обычный startup при
pending intent не инициирует SDK writes и получает существующую storage startup error.
Оба игровых режима, guardian/JVM contract, UI/RAM и updater source не изменены.
SDK update-download fallback при unwritable App пока не устранён: см. [releasing.md](releasing.md).

При повреждении/несовпадении служебных записей автоматическое восстановление запрещено.
В частности, crash между отдельными атомарными записями intent и receipt может потребовать
проверки метаданных. Это fail-closed, не транзакция сразу нескольких файлов. Безопасный
порядок обслуживания описан в [releasing.md](releasing.md); нет команды удаления gate,
таймаутной очистки или принятия неизвестного App.

## Release packaging

Authoritative version хранится в `Directory.Build.props`. `scripts/build-release.ps1` выполняет self-contained `win-x64` publish и pinned `vpk 1.2.161`, создаёт Setup/full/portable/release index и `SHA256SUMS.txt`. Детали: [releasing.md](releasing.md).

### Native bootstrapper и IPC v1

`native/Launcher.Bootstrapper` — Win32 x64 asInvoker, статический CRT, Unicode и
PerMonitorV2. Не использует WPF/.NET/WebView до выбора каталога. Один внешний EXE
содержит uncompressed file payload и versioned hash-pinned binary index/footer;
извлечение только в private уникальный staging выбранного тома. Native проверки
ограничивают **extraction**, не заменяют решения Installation Coordinator.

`Launcher.InstallHelper` — self-contained non-single-file .NET helper, запускаемый
native GUI с explicit HANDLE_LIST для stdin/stdout/stderr. Stdout — исключительно
bounded UTF8 NDJSON (32768 bytes), stderr — `helper.log` в staging. Неполная строка,
invalid UTF8/schema/unknown/duplicate fields, nonce/SID/requestId mismatch и
неожиданная response transition не дают успеха. Каждая команда несёт `v=1`, positive
monotonic `id`, process nonce, `sid`, `command`; только preflight/install используют
`baseDirectory`. Нет полей executable/hash/product/runtime command line.

Команды: inspect → preflight → install (progress) → success → optional launch;
cancel передаёт CTS работающей операции. Responses — inspection/preflight/progress/
typed failure/success/launched. Native state machine требует ordered real Coordinator
stages, но progress `Completed` **не** активирует успешный экран: необходим отдельный
typed success с ожидаемыми App/GameData. Exit0/EOF/deadline не заменяют verification.
Startup/preflight response deadline60s, install inactivity16min; эти UI deadlines
закрывают IPC/cancel и **не** разрешают очистку intent/rollback или смену Storage Root.

Helper проверяет ordinary non-impersonated SID, parent PID/start ticks/session/token
при startup и командах, использует Environment LocalAppData этого пользователя.
Trusted catalog берётся только из payload, проверяет fixed compiled product и каждый
file hash/size. При install используется прежний lease/intent/Job/completion backend.
При launch helper повторно проверяет installation под lease, удерживает safe leaf
EXE handle и освобождает lease **до** запуска для совместимости startup guard.
Executable строго `<App>\current\MinecraftLauncher.exe`, без произвольных аргументов.

EOF/GUI crash отменяет helper operation; crash самого helper остаётся под existing
installation intent/Job recovery. Installer Job/Game Job и StorageRootResolver не
переписаны. Query witness игрового Java Job и lifetime обоих режимов не меняются.
Подмена executable/tree конкурентным злонамеренным процессом **того же пользователя**
не объявляется новой security boundary; private ACL и no-reparse leaf checks не
являются транзакционным filesystem snapshot. Неизвестное состояние fail closed.

Diagnostic helper выбирает отдельный product/LocalData compile-time, без runtime
production override. Tests/native console/diagnostic vendor host не входят в
production bundle. Реальные E2E и release gates: [releasing.md](releasing.md).
