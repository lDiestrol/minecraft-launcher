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

`InstallationCoordinator` в Infrastructure предоставляет managed API для native
Win32 GUI через реализованный отдельный self-contained managed helper и NDJSON IPC.
Packaging GUI/helper описан ниже. `IInstallationBackend` отделяет порядок операций от Windows/FS.
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

До создания staging требуется `FILE_PERSISTENT_ACLS` выбранного локального тома;
FAT/exFAT отвергаются без fallback. `secureNamespace` проверяет owner/DACL каждого
существующего родителя от корня тома и фактический opened handle. Неизвестные ACE,
null DACL и права посторонних на запись/подмену namespace — fail-closed. Текущий SID,
SYSTEM, Administrators и TrustedInstaller являются доверенными Windows authorities;
payload root требует protected DACL только текущего SID/SYSTEM. Существующие ACL
не исправляются автоматически, небезопасный выбранный каталог может быть отвергнут.
Разрешается только создание посторонних siblings без права подмены существующих.
Directory handles с `FILE_LIST_DIRECTORY` и без delete-sharing удерживают identity;
metadata-only handles недостаточны. После extraction повторяются hash/size checks,
все payload files удерживаются read-only handles без write/delete-sharing и namespace
повторно проверяется перед `CreateProcessW`.
Worker/retry используют исходный pinned bundle handle, а не повторно открывают
путь внешнего EXE, который мог измениться после выбора каталога.

`Launcher.InstallHelper` — self-contained non-single-file .NET helper, запускаемый
native GUI с explicit HANDLE_LIST для stdin/stdout/stderr и read-only namespace/payload
pins. Их OS-копии удерживаются helper до выхода, даже после crash/shutdown GUI.
Trusted creation environment передаёт номера pins; helper проверяет их и снимает
`HANDLE_FLAG_INHERIT` до catalog/Setup/launch, затем удаляет служебную переменную
из своего environment. Pins не передаются далее в Launcher/Minecraft; недостоверная
передача не допускает запуска Setup. Это не IPC/server-controlled executable option.
Учтён Windows inheritance flag обычного `Process.Start`:
[первичный исходник .NET10](https://github.com/dotnet/runtime/blob/v10.0.0/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.Windows.cs#L536).
Stdout — исключительно
bounded UTF8 NDJSON (32768 bytes), stderr — `helper.log` в staging. Неполная строка,
invalid UTF8/schema/unknown/duplicate fields, nonce/SID/requestId mismatch и
неожиданная response transition не дают успеха. Каждая команда несёт `v=1`, positive
monotonic `id`, process nonce, `sid`, `command`; только preflight/install используют
`baseDirectory`. Нет полей executable/hash/product/runtime command line.

Команды: inspect → preflight → install (progress) → success → optional launch;
helper поддерживает cancel/CTS, native GUI отменяет через EOF существующего stdin.
Responses — inspection/preflight/progress/
typed failure/success/launched. Native state machine требует ordered real Coordinator
stages, но progress `Completed` **не** активирует успешный экран: необходим отдельный
typed success с ожидаемыми App/GameData. Exit0/EOF/deadline не заменяют verification.
Startup/preflight response deadline60s, install inactivity16min; эти UI deadlines
закрывают IPC/cancel и **не** разрешают очистку intent/rollback или смену Storage Root.

Единственный worker владеет stdin, выполняет все WriteFile и закрывает pipe; общие
mutex не удерживаются во время pipe I/O. UI Close/Cancel/Escape сразу выставляет
stop/shutdown без ожидания mutex или записи; UI Launch только публикует одноразовый
atomic signal. Worker опрашивает его в reader и отправляет ровно одну launch5 после
install3 success; дополнительных writer threads/неограниченной очереди нет.
Закрытие всегда сигнализирует reader, включая активную установку, и не делает
длительный join в UI. GUI остаётся жив до окончания worker; helper wait — 1s.
При зависшем driver/I/O через 5s watchdog завершает **только native GUI** вместе с его
потоками; нет detach, уничтожения app при живом worker, kill Setup/Minecraft или
удаления intent. EOF сохраняет прежний helper cancellation/recovery contract.
При активной установке EOF передаёт прежнюю CTS cancellation helper. GUI может
завершиться до окончания helper/recovery; intent сохраняется, installer/game Job
не завершаются GUI. При отказе создания watchdog timer завершается только GUI.
Retry ожидает завершения старого worker асинхронно. Начальный exit code равен1,
включая закрытие без первой попытки; только typed success последней попытки даёт0.
Buttons закреплены снизу client area, при узком окне расположены вертикально;
paths/status прокручиваются, MinTrackSize/WM_DPICHANGED ограничены monitor work area.

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
production override. Каждый диагностический запуск имеет fresh GUID; только compiled
diagnostic helper передаёт его VendorProbe и возвращает PID/start UTC ticks.
Process creation не доказывает Main: host атомарно публикует immutable
`launch-<GUID>.marker.json` с schema1/GUID/PID/start ticks после Main. Старые файлы
не заменяются и не принимаются за новый запуск; harness ждёт exact match до 30s,
timeout не разрешает cleanup intent. Production Launcher не получает diagnostic args.
Tests/native console/diagnostic vendor host не входят в
production bundle. Реальные E2E и release gates: [releasing.md](releasing.md).


### Ранние diagnostic failures и NTFS root ACL (review 17.10.2026)

Только compiled Diagnostic с `--smoke <base> <evidence> <launchId>` создаёт
`failure.json`. Параметр evidence обозначает **новый** приватный leaf; его parent
должен уже существовать. Все предки, включая корень тома, проверяются и удерживаются
через прежний `secureNamespace(create=false)`. Leaf создаётся атомарно с protected
DACL текущий SID/SYSTEM и проверяется по открытому handle. Existing leaf/collision,
traversal, reparse, небезопасный owner/DACL и том без persistent ACL отвергаются.
До завершения этой проверки запись evidence запрещена: отказ самой подготовки
evidence, token/COM startup не обещает наличие файла и не использует fallback log.

`failure.json` — flat UTF-8 schema1, максимум 16384 bytes. Общие поля:
`schemaVersion`, `launchId` (32 lowercase hex), `stage`, `category`, `detail`.
Stage — фиксированное имя операции; category — `namespace_acl` для typed ACL refusal,
иначе `bootstrapper_error`. Detail — фиксированный текст из кода, без `what()`.
ACL дополнительно содержит normalized `path` фактически проверенного каталога,
`policy` (`directory_namespace` / `private_payload`), `rule`, числовые `allowedMask`
(mapped generic rights) / `rejectedMask` и фиксированный `trustee`:
`authenticated_users` либо `other_or_not_applicable`. Остальные SID, полный descriptor,
helper messages, locator/file contents, environment, команды и токены не копируются.
Путь может содержать имя пользователя; это локальное diagnostic evidence, не telemetry.
Owner/DACL/inheritance/ACE/private-full-control отказы имеют отдельные rule codes.

Worker записывает отказ до отправки GUI event, сохраняя исходную стадию даже при
ошибке обработки события или закрытии окна. Startup после подготовки evidence и
GUI exceptions используют тот же writer. `failure.json`, прежние `selection.bmp`,
`preflight.bmp`, `success.bmp`, `failure.bmp`, `launched.bmp` и `launched.json`
записываются только по фиксированным именам с `CREATE_NEW | FILE_FLAG_OPEN_REPARSE_POINT`,
без share-write/delete; перед каждой записью повторяется audit pinned namespace.
JSON/marker ограничены 16 KiB, bitmap — 32 MiB. Ошибка записи/flush/ACL recheck
поглощается, исходный отказ установки сохраняется; existing evidence не заменяется.
При short write/flush failure возможен неполный файл: evidence best effort,
повторная попытка его не исправляет. Screenshots и Main marker contract сохранены.
Production не получает writer/CLI override, ACL/install/helper/hash решения прежние.

Исторический review до новых отчётов не выявил критической проблемы. По данным
владельца, старый run `2eb6e67196fb42d2876f5aff153b08a1` на D: отказал до extraction.
Для `Authenticated Users: Modify`, mask `0x001301BF`,
пересечение с текущим namespace запретом равно `0x112`: `FILE_ADD_FILE/FILE_WRITE_DATA`,
`FILE_WRITE_EA`, `FILE_WRITE_ATTRIBUTES`. Код сначала проверяет `D:\`, до создания
`.launcher-install`; это согласуется с предполагаемым исключением
`unsafe directory namespace ACL: D:\`. Точное исключение старого запуска не сохранено;
на момент того review новая диагностика ещё не была испытана на этом томе.

Дополнение по предоставленным отчётам домашних Windows10 испытаний сборки
`970588dc`, документировано 17.10.2026: C: clean run
`04f7041153da4f9181335a70c224a1c9` — PASS; D: negative run
`3ff160b27f824ecc8325a4b83ce2e24d` — PASS с ожидаемым GUI exit1 и `failure.json`:
`namespace_audit` / `namespace_acl`, path `D:\`, rule
`untrusted_allow_rights_rejected`, trustee `authenticated_users`, allowedMask `1245631`,
rejectedMask `274`. Это сообщение владельца, не самостоятельная проверка evidence
на Debian; подробности и границы — [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
Отказ D: согласуется с текущим audit корня до staging и не доказывает возможность
установки на этом томе. План оставшихся Cancel/Recovery испытаний —
[releasing.md](releasing.md#план-оставшихся-windows-gui-e2e-cancel-и-recovery).

Важно различать права: эта конкретная mask **не содержит** `FILE_DELETE_CHILD`,
`WRITE_DAC`, `WRITE_OWNER`. Создание файлов в root само по себе не даёт записи в
существующий private child. `DELETE` относится к объекту с ACE, а root volume
не может быть переименован. См. Microsoft [directory rights](https://learn.microsoft.com/en-us/windows/win32/fileio/file-access-rights-constants)
и [rename semantics](https://learn.microsoft.com/en-us/windows-hardware/drivers/ddi/ntifs/ns-ntifs-_file_rename_information).

Protected DACL останавливает [inheritance](https://learn.microsoft.com/en-us/windows/win32/secauthz/security-descriptor-control),
но не доказывает безопасность всей цепочки parent→child. `DELETE` у child или
`FILE_DELETE_CHILD` у parent может разрешить удаление/rename; `WRITE_DAC/WRITE_OWNER`
позволяет изменить boundary. [MS-FSA access/sharing rules](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fsa/82b364ce-6d7b-422f-8d88-4db32eea809a)
описывают в том числе получение DELETE через parent. Обычный промежуточный каталог
с Modify отличается от непереименовываемого volume root.

No-delete handles с `FILE_LIST_DIRECTORY` защищают текущую модель от замены,
а metadata-only pins недостаточны. Однако directory handles с `FILE_SHARE_WRITE`
не блокируют изменение reparse data сами по себе: [MS-FSA FSCTL_SET_REPARSE_POINT](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-fsa/4aeefef8-92c3-4abc-af7a-a610caf8a165)
проверяет `FILE_WRITE_DATA | FILE_WRITE_ATTRIBUTES`. Сейчас эти права у недоверенных
SID запрещены ACL audit. Final-path/hash rechecks — наблюдения, а не атомарная
транзакция последующих pathname operations. Handle сохраняет объект, но не всегда
его имя после допустимого unlink/replacement; POSIX rename требует отдельного анализа.
GUI→helper handle handoff закрывает lifetime gap только в рамках текущих предпосылок.

| Вариант поддержки broad-root NTFS | Угрозы и ограничения |
| --- | --- |
| Сохранить текущую fail-closed политику | Наименьший риск; часть обычных data volumes отвергается. Рекомендуется для PR #14 |
| Внешнее provision безопасного namespace администратором | Требует отдельного разрешения и изменения внешней конфигурации. Один private child не устраняет отказ root в текущем алгоритме |
| Новая secure-anchor модель | Потенциально принимает именно root Modify без delete-child; нужны отдельный design и Windows adversarial tests. Исключение для буквы D: или пропуск root недостаточны |
| Привилегированный broker/provisioner | Может создавать anchor, но добавляет привилегированную attack surface, ownership/recovery/deployment contracts |

Будущий secure-anchor review должен доказать безопасное создание и переход от root
к anchor, учитывать заранее открытые handles, collision и precreated trees, reparse,
rename/delete/POSIX races, file/volume identity и открытия относительно pinned handles.
Pins нужны на всё время операции и у helper, затем отдельные правила после выхода и
reboot. Безопасное staging не доказывает безопасность конечных App/control/recovery/
launch путей. Обязательны атаки другого непривилегированного SID на Windows10/11,
concurrent rename/delete/reparse и явные границы same-SID/admin/SYSTEM/volume remapping.
Эта новая модель в PR #14 не реализована; проверки `secureNamespace()` не ослаблены.
