# Единый журнал Minecraft Launcher

> Storage Root, снимок 11 октября 2026 года, Asia/Yekaterinburg (UTC+05:00):
> **SR-001/SR-002/SR-003 прошли независимое review: READY FOR COMMIT.**
> Владелец согласовал recovery policy для lifetime основной JVM, не произвольных потомков.
> Intent v3/witness v1 интегрирован после проверки Java 17/21; правила v1/v2 отделены.
> Новый полный прогон: **340/340** (Core 329 + App 11), 0 failed/skipped;
> restore/build/format успешны, build 0 warnings/errors. Реальный Minecraft не запускался.
> Дополнительно 16 production-guardian проверок на Java 17/21/25 прошли; подробности ниже.
> Стабильная ветка: `main`; verified commit после `git fetch origin`:
> `bd98ecd721298981508d54466d11089c1647ab4f`.
> Стабильная версия — **v0.6.0**. Следующая программная разработка — **v0.6.1**.
> Исторический полный прогон до исправлений review: **299/299** (Core 288 + App 11).
> Прежние отрицательные prototype gates сохранены как история; актуальные результаты — раздел 12.
> [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8) и
> [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9) объединены в `feat/v0.6.1`.
> [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10) ещё открыт, не merged.
> `feat/v061-storage-root` создана от `52ee0d8`; код/коммиты PR #10 не включены.
> Владелец согласовал один commit, обычный push и Draft PR в `feat/v0.6.1`.
> Актуальные commit/статус публикации: [ветка Storage Root](https://github.com/lDiestrol/minecraft-launcher/tree/feat/v061-storage-root)
> и [PR этой ветки](https://github.com/lDiestrol/minecraft-launcher/pulls?q=is%3Apr+head%3Afeat%2Fv061-storage-root).
> Merge/tag/release запрещены; installer не создан.

## 1. Назначение и источник состояния

Minecraft Launcher — публичный Windows x64 клиент на .NET 10 / WPF / MVVM.
Официальный репозиторий: [lDiestrol/minecraft-launcher](https://github.com/lDiestrol/minecraft-launcher).
Правила всех агентов: [AGENTS.md](../AGENTS.md); совместимая точка входа Claude Code:
[CLAUDE.md](../CLAUDE.md).

Этот Git-tracked журнал — единый источник состояния, согласованного scope, решений,
открытых вопросов и результатов проверок. Нормативные JSON-контракты остаются в
специализированных документах. Сначала проверяйте фактический Git: verified commit
выше является датированным снимком, а не указанием откатить будущий main к этому SHA.
Если код, новый Git или результаты команд расходятся с записью, зафиксируйте расхождение
и обновите журнал; планы не превращаются автоматически в реализованные функции.

Категории сведений:

- **Проверено** — подтверждено кодом, Git, командами или публичным API в указанную дату.
- **Исторически подтверждено** — ранее записанный результат или подтверждение владельца;
  в этой сессии не повторялось.
- **Согласовано** — требования владельца; реализация отдельно отмечается в таблице версий.
- **Предложение** — вариант реализации, требующий design review, без расширения scope.
- **Не проверено** — внешнее состояние или вопрос, который пока нельзя считать решённым.

На ноутбуке рабочая копия находится в `C:\Projetcs\minecraft-launcher` (написание
`Projetcs` намеренное). В старом handoff указан рабочий ПК `F:\Projects\minecraft-launcher`;
его состояние и путь сейчас не проверены. Путь третьего компьютера неизвестен.
На каждой машине проверяется собственный checkout. Синхронизация документов между
машинами происходит через согласованные commit/push и получение Git-изменений;
получение этой документационной ветки другими машинами проверяется отдельно.

## 2. Стабильный выпуск и проверенная основа

| Параметр | Проверенное значение |
| --- | --- |
| Origin | `https://github.com/lDiestrol/minecraft-launcher.git` |
| Стабильная ветка | `main`, verified `origin/main` — `bd98ecd721298981508d54466d11089c1647ab4f`; рабочий feature HEAD отслеживается отдельно |
| Интеграционная ветка | `feat/v0.6.1`, verified `origin/feat/v0.6.1` — `52ee0d8d151e7e55327c20376a9678e78b3cc5c1`; включает объединённые PR #8/#9 |
| Verified main | [bd98ecd721298981508d54466d11089c1647ab4f](https://github.com/lDiestrol/minecraft-launcher/commit/bd98ecd721298981508d54466d11089c1647ab4f) |
| Стабильный release | [v0.6.0](https://github.com/lDiestrol/minecraft-launcher/releases/tag/v0.6.0), GitHub API: `draft=false`, `prerelease=false`, опубликован 09.10.2026 |
| Release tag/target | [1ae3ae74b1b70d92031fe8706f80f85e506efd56](https://github.com/lDiestrol/minecraft-launcher/commit/1ae3ae74b1b70d92031fe8706f80f85e506efd56) |
| Версия исходников | `0.6.0` в [Directory.Build.props](../Directory.Build.props) |
| Runtime/packaging | Self-contained `win-x64`, Velopack `1.2.158`, PackId `lDiestrol.MinecraftLauncher`, stable channel |
| Подпись | Authenticode отсутствует; пакеты unsigned |

Tag релиза и verified main различаются: после релизного commit в main добавлена
документация через PR #7. `git diff v0.6.0..origin/main` показывает только документацию.
Названия старых feature/release-веток не задают актуальную основу разработки.

Проверенный снимок Git/GitHub на 11.10.2026: `origin/main` — `bd98ecd`,
`origin/feat/v0.6.1` — `52ee0d8`,
`origin/release/v0.6.0` — `e426ecb`. [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8)
объединён в `feat/v0.6.1` с merge commit `c31c982`; [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9)
также объединён в `feat/v0.6.1` (`52ee0d8`). PR #10 открыт Draft из
`feat/v061-singleplayer-launch` в `feat/v0.6.1`, опубликованный head — `c604955`.
Новая независимая `feat/v061-storage-root` создана от актуального `52ee0d8`,
а не PR #10; исходное дерево было чистым. Исторический HEAD на этапе разработки и
read-only review оставался этим baseline; публикационный HEAD проверяется по Git/ветке.
Статусы PR и HEAD изменяемы; перед следующей работой их следует проверить заново.

Согласованная 10.10.2026 Git-стратегия: main сохраняет стабильную v0.6.0;
промежуточные feature/docs/fix PR объединяются в `feat/v0.6.1` только в рамках
разрешённой задачи. Полностью завершённая v0.6.1 позже переносится в main одним
Squash & Merge после явного согласования владельца. Последующее объединение PR #9
подтверждено Git/GitHub. После независимого review владелец разрешил публикацию Storage Root
одним commit, обычным push и Draft PR; merge/tag/release остаются запрещены.

## 3. Версии и отдельные направления

| Версия / этап | Состояние | Объём и подтверждение |
| --- | --- | --- |
| MVP-1 | Завершён | Shell/onboarding; исторический commit `664b6ed` |
| MVP-2 | Завершён | Minecraft/Fabric launch; старый branch snapshot `e77be5f` содержит 125 тест-кейсов |
| MVP-3 / pack sync | Уже реализован в стабильной основе | Pack Manifest v1, SHA-256 sync, staging, Repair; начинать этап заново нельзя |
| 0.4.0-rc.1 / 0.5.0-rc.1 | Исторические release notes | Сохранены в Git; публикация этих RC сейчас отдельно не проверялась |
| v0.5.0 | Завершён, исторический stable | Tag `v0.5.0`, commit `c0e3990`; основа проверенного ранее installed update |
| v0.6.0 | Текущий stable, опубликован | Dark Gaming UI, profile file management, locks/guardian, app updates и release tooling |
| v0.6.1 | Частично реализован, не выпущен | Два режима — в отдельном Draft PR #10, не в этой ветке. Здесь локально реализован Storage Root. GUI установка/выбор диска, independent App/GameData, адаптация путей и реальные clean-install/installed update остаются release gate |
| v0.6.2 | Пересмотренный scope владельца | UI/UX: компактный адаптивный Dashboard, оконный/maximized режим, тёмные ScrollBar, карточки, точный RAM + slider, реальные Server List Ping/players/slots, timeout/no-data, статусы/ошибки |
| v0.6.3 | Согласован, запланирован | Storage Manager: размеры/ownership, managed removal/shared cleanup, сохранность worlds/user files, перенос и отдельные каталоги профилей без удаления используемых shared-файлов |
| v0.7.0 | Согласован, запланирован | Несколько источников, source-aware profile identity без коллизий, каталог/поиск/избранное, управление сборками и совместимое развитие news/feed без нарушения строгих v1 схем |
| Серверный Pack Manager | Отдельный согласованный трек проектирования | Publisher, версионирование клиентских pack, планируемый LXC/NGINX; реализация и deployment не подтверждены |
| Единая документация агентов | Объединена в `feat/v0.6.1` | [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8) объединён 10.10.2026; merge commit `c31c98293cec665c5bffa7022263a4ffc7661bbc` |

## 4. Что уже работает в v0.6.0

**Проверено по коду и тестам:** onboarding и восстановление подключения; строгий
Server Protocol v1; профили, offline nickname и RAM slider; точные Minecraft/Fabric
версии и managed Java через CmlLib.Core `4.0.6`; Pack Manifest v1; полный SHA-256
sync/Repair; Dark Gaming UI, адаптивная компоновка и WindowChrome.

Play синхронизирует pack до запуска и передаёт серверный адрес/порт. Repair выполняет
тот же hash/sync cycle без запуска. Открываются папки instance, mods, resourcepacks
и shaderpacks. Удаляются только известные managed pack-файлы из valid state;
это **не полное удаление установки профиля** и не очистка общих Minecraft/Java-файлов.
Изменённый пользователем файл, чей путь принадлежит manifest/state, всё ещё managed:
Sync/Repair восстанавливает опубликованную версию, а managed removal может его удалить.

`LauncherOperationCoordinator` защищает Play/Repair/Delete профильно через
`FileProfileOperationLock`. Разные профили могут работать одновременно;
`IsActive` учитывает активные операции для блокировки app update в текущем процессе.
`FileSharedInstallLock` сериализует подготовку общих CmlLib-файлов между процессами.
Guardian запускает Java и удерживает профильную защиту до выхода игры, включая
сценарий закрытия Launcher. Отмена до старта поддерживается; после решения о старте
выполняется защищённая передача guardian, а cancellation не убивает Minecraft.

Обновления приложения используют закреплённый GitHub Releases source, без token.
После показа окна startup check идёт неблокирующе; ошибка/отсутствие update не меняют
основной UI. Есть ручная проверка в Settings и dismissible banner. Download/apply
требуют действия пользователя. Источник и channel не приходят из server config.
Installed channel использует Velopack package state; вывод channel в UI ещё относится
к UI/UX v0.6.2. Начальная фраза ViewModel о ручной проверке не описывает уже реализованный
startup check — это известная UX-несогласованность.

**Исторически подтверждено владельцем (09.10.2026):** установка stable v0.5.0,
обнаружение и скачивание v0.6.0, apply/restart, сохранение settings и успешный Minecraft
launch. Этот installed E2E в текущей сессии не повторялся. Portable A→B apply E2E
v0.6.0 отдельно не выполнен; Velopack portable mode поддерживается кодом.

**Внешний snapshot, сейчас не перепроверен:** WT #1 — Minecraft `26.1.2`, Fabric
`0.19.3`, packVersion `1.0.1`. Эти значения были записаны 09.10.2026, приходят из
production JSON и не определяются версией Launcher. Production URL/пути, действующая
конфигурация NGINX, VM и резервные копии в этой задаче не исследовались и не менялись.

Ограничения стабильной версии: Windows x64, Fabric, offline nickname; managed pack
только `mods/` и `config/`. Microsoft login, Forge/NeoForge/Quilt, telemetry,
backend/admin panel и управление resourcepacks/shaderpacks через manifest отсутствуют.

## 5. План v0.6.1 — согласованный scope

Владелец пересмотрел scope: два режима одного профиля + правильная установка с выбором
диска и первоначальным GameData на том же диске, отдельно от App, shared reuse и
совместимостью установленного обновления v0.6.0. Server Protocol/Pack Manifest v1 и
старые settings остаются совместимыми. GUI installer в этом программном этапе не создаётся.
Редизайн Dashboard, точный RAM, ping/players — v0.6.2; полный Storage Manager и
произвольный перенос существующих установок — v0.6.3, не v0.6.1.
История: прежний scope v0.6.1 содержал точный RAM/UX, а Storage Manager был v0.6.2;
ниже эти требования сохранены с пометкой переноса. Подробная roadmap-финализация
также находится в отдельном PR #10; его коммиты не переносились в эту ветку.

### 5.1. Серверный и одиночный запуск

- Основная кнопка сохраняет серверный запуск и автоматическое подключение.
- Дополнительная кнопка «Одиночная игра» использует тот же выбранный профиль,
  instance, Minecraft, Fabric, pack sync, mods/config, nickname и RAM.
- Одиночный режим открывает главное меню: не передаёт server address/port и
  quick-play server arguments. Отдельной schema или локального profile не создаётся.
- Оба режима сохраняют Play/Repair/Delete/update ограничения, profile locks,
  shared-install lock, guardian, cancellation, process lifecycle и обработку ошибок.

Проверки: launch request отличается намерением подключения; серверное поведение
сохраняется, одиночные CmlLib options не содержат endpoint; pack failure запрещает
оба запуска; второй Launcher не запускает тот же профиль; cancellation/exit
освобождают соответствующие leases без преждевременного снятия защиты игры.

Одиночный запуск уже опубликован в PR #10; здесь только интеграционная основа без
его реализации. Пользовательский smoke владельца 10.10.2026 подтвердил главное меню
Singleplayer и автоподключение Server; это не CI и не проверка installer/update/worlds.

### 5.2. Точный RAM — прежний scope, перенесён в v0.6.2

- Slider остаётся; рядом появляется числовое поле с точностью 1 МБ.
- Ручное значение (например, 3073 МБ) не округляется до 1024 МБ или шага slider.
- Поле и slider синхронизируются без потери ручной точности; движение slider
  сохраняет привычный дискретный шаг.
- Сохраняются безопасные границы физической RAM. Сейчас `RamPolicy` задаёт минимум
  2048 МБ, потолок не выше 16384 МБ и reserve 3/4/6 GiB; `Normalize` и `IsValid`
  требуют кратности 1024. Именно это ограничение точности нужно изменить при реализации.
- Точное значение хранится в существующем `RamMb` в settings без изменения формата;
  старые настройки загружаются без миграции.
- Пустой, ошибочный и выходящий за диапазон ввод получает понятную валидацию.

Проверки: 3073 МБ сохраняется/reloads и доходит до запуска без округления;
min/max и малая физическая RAM учитываются; slider и поле не зацикливают обновления;
keyboard/focus/DPI остаются удобными. Предложение: одно точное числовое значение
является источником состояния, slider — способом его изменения.

### 5.3. Прежний план UX/порядка работ — исторический, не текущая очередь

Следующие пункты сохраняют старое решение; RAM/UX теперь v0.6.2.
Текущий следующий этап — GUI installer после review Storage Root, а не повторная реализация launch modes.

Согласованы объяснения блокировок Play/Repair/Delete, понятные ошибки с деталями в
локальном логе и реальный channel `stable`/`dev` из package/runtime state.
Фиктивные online/ping/player count и новые server fields не добавляются.

1. После отдельного разрешения на программную задачу создать feature-ветку от актуальной
   `feat/v0.6.1`, сверив также стабильный `origin/main`.
2. Исследовать request/coordinator/CmlLib options; добавить намерение запуска и тесты Core.
3. Сохранить server path, добавить singleplayer и вторую кнопку в адаптивном UI.
4. Реализовать точный RAM и persistence tests; допустим отдельный PR внутри v0.6.1.
5. Выполнить ограниченные UX-исправления и проверки layouts/DPI.
6. Пройти build/tests/format, ручной smoke обоих режимов, settings/restart,
   release feed и installed update path именно новой версии.

Предложение для review: Core enum/value object `Server`/`Singleplayer`; UI не должен
управлять CmlLib flags напрямую. Названия типов и разбиение PR пока не зафиксированы.
Результаты диагностики и локального исправления WPF-теста приведены ниже;
они не разрешают произвольное расширение функционального scope v0.6.1.

### 5.4. v0.6.1 — реализованный локальный Storage Root этап

- Общий `StorageRootResolver` для App/guardian, versioned per-user указатель
  `%LOCALAPPDATA%\MinecraftLauncherControl\storage-root.json`, не внутри App.
  Только schemaVersion 1/gameDataRoot, strict JSON до 16 KiB; запись unique temp,
  flush-to-disk и same-volume atomic rename. Settings/logs пока остаются в GameData.
- `OpenSession()` без указателя сохраняет legacy `%LOCALAPPDATA%\MinecraftLauncher`
  без миграции. Настроенный root проверяется на существование/права/reparse boundaries;
  missing drive/invalid pointer/ACL denial — понятная ошибка без fallback и пересоздания.
- Shared session lease в Control живёт до выхода App/guardian, exclusive configuration
  lease не допускает смены root в этот период. `ConfigureNewRoot()` — только initial
  placement пустого каталога или закрепление legacy; root changes/чужие данные запрещены.
  Нет root UI/CLI, копирования/переноса данных или установщика.
- GameData вне App/Control и их предков, не volume root/UNC/device path; проверяются
  все существующие ancestor reparse points. App/guardian получают одинаковый trusted root
  до request validation; request.RootDirectory/server JSON не могут выбирать root.
- `LauncherDataPaths` передаётся CmlLib/PackSync/settings/logger/locks/file manager;
  assets/libraries/versions/runtime общие, instances отдельные. Same-volume pack staging
  и existing guardian/profile/shared locks сохранены, есть guard после потери root.
  Dotnet-host development guardian получает local entry assembly, без server launch flags.
- Tests: root persistence, legacy settings/worlds, missing volume/root, strict JSON,
  Windows ACL denial, junction ancestors/Control, session contention (в том числе отдельный
  PowerShell process), profile/shared lock identity, CmlLib shared mapping, PackSync repair
  и target-volume staging, guardian trust/lifetime после закрытия Launcher.

Ограничения: один root на пользователя для installed/portable/development;
унаследованные per-user права Control, без all-users installer support. Не обещается
защита от произвольного same-user tampering/TOCTOU или полный pack rollback.
Реальные Minecraft/custom-directory install/update E2E ещё не выполнялись.
Следующий PR должен выбрать GUI-путь и связать его с initial ConfigureNewRoot до
первого запуска под правильным пользователем. Pinned Velopack capabilities/deprecation
и предложение отдельного upgrade approval: [releasing.md](releasing.md).

## 6. Storage Manager — прежняя v0.6.2, перенесено в v0.6.3

Нижеследующие ownership/cleanup требования сохранены; теперь это отдельный этап v0.6.3.

После завершения v0.6.1 показать объём выбранного профиля и отдельно общих данных,
добавить безопасное удаление **установки профиля** и отдельное управление общими
`assets`, `libraries`, `versions` и managed Java/runtime.

- По умолчанию сохранять `saves`, `screenshots`, пользовательские resourcepacks,
  shaderpacks, options и unmanaged mods; не путать их с manifest-managed путями.
- Учитывать использование общих файлов другими профилями; не приписывать весь
  Minecraft одному instance и не суммировать общие файлы повторно.
- Запрещать удаление при Minecraft/sync/Repair/конфликтующей операции и показывать
  точное подтверждение, что будет удалено, что сохранится и какой объём освободится.
- Все удаления ограничивать проверенными корнями без symlink/junction/reparse traversal.
- Следующий Play после удаления должен восстановить нужные managed-файлы.

Предложенный порядок: read-only inventory → согласованная ownership-модель →
preview/removal профиля → отдельная shared cleanup → тесты и ручные smoke.
Reference index общих файлов вводить только если использование нельзя безопасно
определить без него. Эти варианты ещё требуют design review.

Тесты используют временные каталоги: traversal/reparse, сохранение личных данных,
изоляция других профилей, shared accounting, cancellation, частичный отказ удаления
и последующая загрузка. До реализации нужно решить, что считать установкой сверх
managed pack, как отличать пользовательские config и какая recovery/rollback policy нужна.

## 7. Pack Manager — независимый серверный трек

Детальный согласованный план и вопросы: [PACK_MANAGER_PLAN.md](PACK_MANAGER_PLAN.md).
Это отдельное направление, не часть v0.6.1/v0.6.2 без решения владельца.

Согласованы публикация/версии клиентских pack, draft/inventory, фактические SHA-256 и
size, path/mod duplicates validation, immutable published versions, atomic publication,
diff preview, audit/history и rollback на проверенную версию. Launcher application
version и GitHub Release от публикации game pack не меняются.

Планируется лёгкий LXC на Proxmox для JSON и клиентских pack с NGINX HTTPS reverse
proxy, сохранением публичных URL и совместимостью Launcher v0.6.0 / строгих v1 схем.
Создание LXC и миграция ещё не выполнены и требуют отдельного разрешения.
Сначала общее publishing core/API или command boundary, затем возможный WPF Admin
либо private web UI; конкретный интерфейс и API/CLI ещё не выбраны.
Обычный Launcher не получает admin endpoints, SSH/SFTP или credentials.

Предлагаемая последовательность: инвентаризация текущего hosting/backup/ownership →
локальный Publisher на копии данных → LXC/shadow hosting → backup и безопасное
переключение NGINX со smoke/rollback → server pack coordination.

Последующее управление серверными модами — отдельный этап: read-only inventory VM,
отдельная server pack version, staging, проверка, backups, explicit stop/restart,
health check и rollback; client-only/server-only/common моды согласуются совместно.
Пока неизвестно, systemd или ручной запуск управляет VM, lifecycle не автоматизируется.
Постоянный агент на Minecraft VM на первом этапе не нужен.

Предложения, не обязательная реализация: content-addressed storage, отдельный signing
artifact, REST API против CLI/queue, порядок WPF/web UI, mod-side detection,
retention и read-only systemd timer. Все инфраструктурные операции требуют отдельной задачи.

## 8. Принятые архитектурные решения

| Решение | Действующее правило / источник |
| --- | --- |
| .NET 10 / WPF / MVVM | Core независим; App и Infrastructure зависят от Core; composition root явный |
| Раздельные lifecycle | Velopack app update, CmlLib runtime preparation и game pack sync независимы |
| Application/data roots | Stable defaults: `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher` / `%LOCALAPPDATA%\MinecraftLauncher`; локальный Storage Root этап допускает отдельный выбранный GameData и независимый per-user Control (раздел 5.4) |
| Profile/shared storage | `instances/<id>` и общие `assets`, `libraries`, `versions`, `runtime` |
| Managed ownership | `.launcher/managed-state.json`, недоверенный state проверяется; пользовательские unmanaged пути сохраняются |
| Pack validation | SHA-256 при каждом Sync/Repair, exact size, staging, атомарная замена каждого файла; полный pack rollback отсутствует |
| HTTP boundary | HTTPS/loopback, ручные проверяемые redirects, строгий JSON; metadata budget 30 секунд отдельно от payload transfer |
| Concurrency | Profile operation lock, guardian protection до выхода игры; shared CmlLib install lock |
| Server Protocol / Pack Manifest | Отдельные строгие схемы v1; неизвестные поля запрещены |
| App update source | Только закреплённый GitHub repository, channel из package metadata, explicit download/apply |
| Release | Version из Directory.Build.props; current-release-only feed; опубликованные пакеты не заменяются |
| Git integration | `main` — stable v0.6.0; промежуточные PR → `feat/v0.6.1`; готовая версия → main одним согласованным Squash & Merge |

## 9. Известные проблемы, расхождения и технический долг

| Запись | Факт и действие |
| --- | --- |
| WPF test failure, 10.10.2026 — исправление объединено | До исправления `CaptionButtonsExecuteTheirWindowActions`, assertion line 63: ожидалось `ActualHeight=41`, реально `41.333333333333336`. Диагностика на актуальной `feat/v0.6.1` подтвердила DPI 150% и унаследованный `UseLayoutRounding=True`: 41 DIP округляется до 62 физических пикселей, затем 62 / 1.5 DIP. DPI-aware исправление объединено через [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9); действия кнопок сохранены, UI не менялся, предыдущий полный прогон — 252/252 |
| Исторические 252 passed | Записаны для релиза 09.10; диагностические 251 passed/1 failed на ноутбуке и последующие 252 passed после исправления теста — отдельные результаты, не переписывающие историю релиза |
| Atomic publish терминология | File.Replace/Move атомарны пофайлово; нет journal/backup/rollback всей pack-транзакции. I/O failure или crash в apply могут оставить частично обновлённый pack и старый state. Нельзя обещать полный crash rollback |
| Длина архитектурных классов | MainViewModel/PackSyncService совмещают много обязанностей; возможный рефакторинг — технический долг, не согласованная новая функция v0.6.1 |
| Startup update сообщение | Начальная фраза ViewModel о ручной проверке расходится с автоматическим startup check; записано для UX review |
| Portable/code signing | Portable apply E2E не повторён; Authenticode отсутствует; решение о подписи и UX остаётся открытым |
| Старые документы | Разные baseline SHA и путь ПК в status/handoff; сохранены как исторические снимки, текущие правила/планы перенесены сюда |
| Исправления документации | Уточнены code-behind, профильно scoped coordinator/guardian, pack sync в launch flow, пофайловая atomicity и фактическое имя Setup asset |
| Release notes v0.6.0 | Фраза «после публикации» — исторический текст релиза; актуальная публикация подтверждена API. Notes сохранены без переписывания истории |

Историческая диагностика WindowChrome (10.10.2026): исходное рабочее дерево было чистым;
создана локальная `fix/v061-windowchrome-dpi` от актуальной `origin/feat/v0.6.1`.
У всех трёх кнопок заданы `Width=46`, `Height=41`; измерены `DpiScaleX/Y=1.5`,
`ActualWidth=46`, `ActualHeight=41.333333333333336`. На отдельном экземпляре окна
переключение layout rounding только в памяти дало последовательность высот
41.333333333333336 → 41 → 41.333333333333336. Hit testing в центре кнопок,
enabled/focusable/chrome flags и действия minimize/maximize/restore/close проверены.
Изолированный WPF layout с DPI 100/125/150/175/200% подтвердил формулу
`Math.Round(size * dpiScale) / dpiScale` для обеих осей; она соответствует
[исходникам WPF 10.0.12](https://github.com/dotnet/wpf/blob/v10.0.12/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/UIElement.cs).
После согласования изменён только `WindowChromeBehaviorTests.cs`: новый helper для
всех трёх кнопок строго проверяет заданные 46×41 DIP и включённый layout rounding;
ожидаемые ActualWidth/ActualHeight рассчитывает по DPI конкретной кнопки и оси.
`MidpointRounding.ToEven` сверено с загруженной WPF 10.0.12: 61.5 → 62,
57.5 → 58 и 80.5 → 80 физических пикселей. Допуск сравнения — один ULP ожидаемого
double (`Math.BitIncrement(expected) - expected`), здесь около 7.1e-15 DIP;
это погрешность представления, а не допуск в физический пиксель.
WindowChrome, IsEnabled, Focusable и действия кнопок сохранены. UI не менялся.
Результаты того локального этапа и ограничения multi-DPI проверки — в разделе 12.

## 10. Нерешённые вопросы

Перед v0.6.1: независимое review Storage Root/PR #10 и последующая совместная проверка;
GUI installer choice, per-user identity/hook ordering, app install location, временные
package paths и реальные clean-install/installed update. Pinned MSI не подтверждает
готовый GUI folder picker. NuGet deprecation/SDK+vpk 1.2.161 — отдельное согласование.

Перед v0.6.2: точный RAM clamp/validation, компактная компоновка/ScrollBar,
реальные ping/players/slots и timeout/no-data; channel и сообщения блокировок.
Перед v0.6.3: состав установки профиля; managed config/личные настройки; shared-file
usage и preview, cleanup/rollback, безопасный перенос и отдельные roots профилей.
Перед v0.7.0: source-aware identity/migration и совместимость строгого Server Protocol v1.

Для Pack Manager: фактические public URL, NGINX roots/cache/invalidation,
ownership/permissions и текущий publishing source; backup/retention/rollback;
LXC OS/quota/storage/network/firewall/service credentials; VM lifecycle и health timeout;
client/server/common mod mapping; authoritative pack version, publisher/reviewer roles,
storage atomic primitives и требуемый audit export. Детали остаются в отдельном плане.

Прочие вопросы: review/дальнейшая интеграция PR #10 и Storage Root; portable update UX/E2E;
code signing и срок; частые реальные диагностические ошибки блокировок.
Необходимость ADR и машинной release checklist — предложения, не scope v0.6.1.

## 11. Журнал работ

| Дата (UTC+05:00) | Работа / свидетельство |
| --- | --- |
| 25.09.2026 | Initial repository и MVP-1: [36b1e73](https://github.com/lDiestrol/minecraft-launcher/commit/36b1e730338e123c922453bb6993f1bddf3f5b51), [664b6ed](https://github.com/lDiestrol/minecraft-launcher/commit/664b6ed551cc28488e46f97491f68b0a073cb2c1) |
| 07.10.2026 | Stable v0.5.0: [c0e3990](https://github.com/lDiestrol/minecraft-launcher/commit/c0e3990633a3bb93a01631d2437e999edaa3d306) |
| 09.10.2026 | Dark Gaming/profile management: [da1b97b](https://github.com/lDiestrol/minecraft-launcher/commit/da1b97b64079240482792faeadf72e478fc236e5); release preparation: [9fa5e89](https://github.com/lDiestrol/minecraft-launcher/commit/9fa5e89b716a2b7fb927e5f7b5cf475289a41738) |
| 09.10.2026 | Feed asset scope и release target v0.6.0: [1ae3ae7](https://github.com/lDiestrol/minecraft-launcher/commit/1ae3ae74b1b70d92031fe8706f80f85e506efd56); ранее записаны 252 passed, build/format/audit/feed и пользовательский installed E2E |
| 09.10.2026 | Status/roadmap/server plan в main через [PR #7](https://github.com/lDiestrol/minecraft-launcher/pull/7), [bd98ecd](https://github.com/lDiestrol/minecraft-launcher/commit/bd98ecd721298981508d54466d11089c1647ab4f) |
| 10.10.2026 | Ноутбук: чистая `feat/mvp2-game-launch` (`e77be5f`) → fetch → main `bd98ecd`; проверены публичный release и ветки, restore/build/test/format/feed |
| 10.10.2026 | Владелец разрешил документационную ветку несмотря на 1 WPF failure; создана `docs/unified-agent-state`, добавлены AGENTS/CLAUDE/Ledger, исправлены ссылки и описания. На первоначальном этапе это был локальный черновик без commit и PR; удаления не выполнялись |
| 10.10.2026 | Затем согласована интеграция через `feat/v0.6.1`; fetch подтвердил ветку от `bd98ecd`. Создан документационный commit, ветка `docs/unified-agent-state` отправлена в origin и опубликован [Draft PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8) — `docs: unify agent instructions and project state`, base `feat/v0.6.1`. На этом историческом этапе merge ещё не выполнялся |
| 10.10.2026 | По замечанию P2 независимого review исправлены устаревшие записи о публикации: сохранена хронология локального черновика и последующего Draft PR #8. Проверены Markdown, внутренние ссылки и `git diff --check`; изменён только этот Ledger, код и WPF-тест не исправлялись |
| 10.10.2026 | В `fix/v061-windowchrome-dpi` воспроизведён WPF-сбой: отдельный тест 0/1, полный прогон Core 241/241 и App 10/11 (всего 251/252). Причина подтверждена измерением DPI, обратимым переключением rounding в памяти и изолированным layout при пяти DPI; действия всех caption buttons работают. Исходники не менялись, исправление предложено на согласование |
| 10.10.2026 | Владелец согласовал исправление теста; в той же fix-ветке добавлены строгие проверки заданных размеров и DPI-aware фактических размеров всех трёх кнопок. Restore/build/format успешны, отдельный тест 1/1, полный прогон 252/252 без пропусков; compiled helper проверен на пяти DPI и двух смешанных X/Y масштабах. На момент завершения этого локального этапа были изменены только тест и Ledger, публикация ещё не выполнялась |
| 10.10.2026 | GitHub подтвердил последующее объединение [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8) в `feat/v0.6.1` (`c31c982`) и публикацию исправления теста в [Draft PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9) (`e7aafe7`, `fix/v061-windowchrome-dpi` → `feat/v0.6.1`). Независимое review подтвердило реализацию DPI-aware проверки и выявило P2: Ledger сохранял предпубликационные формулировки как текущие; эта документационная задача исправляет разделение истории и актуального снимка |
| 11.10.2026 | Fetch подтвердил последующее объединение PR #9: актуальная интеграционная основа `52ee0d8`, main `bd98ecd`, release `e426ecb`. PR #10 открыт Draft, head `c604955`, не merged. Из чистой `feat/v061-singleplayer-launch` создана независимая `feat/v061-storage-root` от origin/feat/v0.6.1 без переноса коммитов PR #10 |
| 11.10.2026 | По разрешению реализован локальный Storage Root: versioned pointer, fail-closed resolver/session для App/guardian, legacy preservation, initial placement API, lease contention, path/reparse checks и development dotnet host. Новые filesystem/guardian/CmlLib/PackSync tests. UI/launch modes/RAM/protocol/dependencies не менялись. Commit/push/PR/merge/tag/release не выполнялись |
| 11.10.2026 | Read-only исследование pinned Setup/vpk/MSI 1.2.158 и NuGet CriticalBugs deprecation: --installto/MSI подтверждены, штатный GUI folder picker не подтверждён. Связанная OS architecture regression исправлена 1.2.161; upgrade SDK/vpk предложен отдельно, но не выполнен. Installer не создан |
| 11.10.2026 | Владелец согласовал вариант A для SR-001. Добавлен локальный прототип durable intent v2 и атомарного CreateProcessW с JOB_LIST/HANDLE_LIST, без KILL_ON_JOB_CLOSE, breakaway или fallback. Отдельный test-only guardian принудительно завершается в трёх фазах. Crash tests выявили принципиальное ограничение: имя temporary Job Object исчезает при закрытии последнего handle, хотя child остаётся жив. Отсутствие имени не доказывает отсутствие игры; до записи PID прототип может ошибочно очистить intent. Реализация остановлена до обходных изменений, SR-001 P1 остаётся открыт |
| 11.10.2026 | Локально добавлены canonical drive/UNC/device проверки SR-002 и защита settings/log leaf SR-003 через OPEN_REPARSE_POINT с проверкой opened handle; settings использует unique CreateNew temp. Network classification unit test использует injected DriveType, реальный SMB mapped drive не проверен. Пять symlink cases сначала не смогли создать ссылки в sandbox; повтор вне sandbox — 5/5 passed, внешние sentinel files сохранены. Commit/push/merge/tag/release, installer и интеграция PR #10 не выполнялись |
| 11.10.2026 | Владелец согласовал сначала самостоятельный прототип уточнённого A: query-only witness через HANDLE_LIST одновременно с JOB_LIST. Создан отдельный tests/StorageRoot.JobPrototype без зависимостей на Launcher и без включения в solution/packaging. Реальная managed Java 25.0.1 сохранила handle с access 4 и пережила аварийное завершение owner; независимый query process открыл Job. Обычный Java ProcessBuilder передал этот же private handle дочерней Java с access 4. По условию владельца реализация остановлена до интеграции и обходных изменений. Наследование в descendant само по себе не доказывает выход из Job или нарушение сохранности данных; строгая граница «только исходная Java» не подтверждена |
| 11.10.2026 | Затем владелец явно разрешил QUERY witness в descendants того же Job. Самостоятельный prototype расширен: реальные Java 8/25 в трёх crash-фазах, immediate crash до JVM-ready, список PID/членство/права/flags, canary и unrelated process от того же owner, наследующий descendant и direct handleless child (Java 25 FFM), nested/restrictive Job, DACL denial и name collision. Живая основная JVM не потеряла witness. Handleless descendant остался жив внутри Job после JVM exit, но имя исчезло. Это допустимо отмечаемая граница защиты дерева, а не повтор прежнего запрета наследования; безопасная автоматическая очистка pre-PID intent не доказана. Уточнённый witness не интегрирован, production diff и SR-002/SR-003 сохранены. Подробные новые результаты ниже |
| 11.10.2026 | После предыдущего stop владелец явно согласовал recovery при отсутствующем Job и завершённом guardian для подтверждённого witness-контракта основной JVM. Проверены Java 17/21, интегрированы intent v3/witness v1 и атомарный запуск со stdio + QUERY witness. Реальные production-guardian crash/recovery checks прошли; legacy/unknown intents и AccessDenied fail-closed. Сохранены SR-002/SR-003, исправлен точный exception contract ProfileFileManagerTests без ослабления containment/reparse проверок; новые результаты 340/340 ниже. Никакой публикации или интеграции PR #10 |

SHA собственных документационных коммитов в Ledger не записываются;
их следует проверять по Git и истории PR.

Публикационный этап, 11.10.2026: независимый агент подтвердил READY FOR COMMIT без
P0/P1/P2/P3 findings. Его собственные прогоны: 340/340 xUnit, 32/32 межпроцессных
сценария, build 0 warnings/errors, format/diff-check чистые и 38 внутренних ссылок.
SHA всех 39 файлов и Git state сохранились после read-only review. Владелец разрешил
один commit `feat: add crash-safe configurable game storage root`, обычный push ветки
и Draft PR в `feat/v0.6.1`. При подготовке публикации изменены только эти метаданные
Ledger; проверенный код не менялся, полный regression повторно не запускался.
Фактический HEAD/номер/статус PR берутся из Git и ссылок публикации выше; own commit
SHA не дублируется в Ledger. Minecraft/Fabric smoke, installer/update E2E и совместная
проверка после PR #10 остаются release gates, а не выполненными проверками.

## 12. Последние проверки

Среда: Windows x64, .NET SDK `10.0.401`, runtimes `10.0.12`.
До исправления теста проверялся verified main `bd98ecd`; на этапе первоначальной
документационной задачи после него изменялась только документация.

| Историческая проверка 10.10.2026 до исправления теста | Результат |
| --- | --- |
| `git fetch origin`, remote/main/tag | Успешно; origin и SHA подтверждены, начальное tracked/untracked дерево чистое |
| `dotnet restore MinecraftLauncher.sln` | Успешно; потребовался доступ вне песочницы к пользовательской NuGet configuration/cache |
| `dotnet build MinecraftLauncher.sln --no-restore` | Успешно; 0 warnings, 0 errors |
| `dotnet test MinecraftLauncher.sln --no-build --no-restore` | Core: 241/241; App: 10/11; всего 251 passed, 1 failed, 0 skipped. Повтор вне песочницы: тот же результат |
| `dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore` | Успешно, exit 0; исходники не форматировались |
| `tests/ReleaseFeedScriptTests.ps1` | Успешно; отдельная script-проверка, не добавляется к 252 .NET cases |
| Markdown/внутренние ссылки/секреты/`git diff --check` | Проверены 17 Markdown-файлов и 74 внутренних файловых ссылки; fences/encoding/targets корректны. Проверка структуры таблиц, diff и новых файлов прошла; в 12 изменённых/новых документах не найдены credential/private-address patterns. Изменений кода, удалений и staged changes нет |
| Packaging, NuGet audit, installed/portable E2E | В текущей сессии не выполнялись; прежние результаты отдельно указаны выше |

Первоначальные fetch/restore внутри песочницы встретили ограничения доступа;
повтор с разрешённым доступом прошёл. WPF failure воспроизводился вне песочницы,
поэтому его нельзя было списать на эти ошибки окружения. Первоначальная документационная
работа сама по себе не являлась исправлением теста или основанием выпускать следующую версию.

Исторические проверки этапа исправления, 10.10.2026, локальная `fix/v061-windowchrome-dpi`:

| Проверка | Результат |
| --- | --- |
| Git/fetch/scope | До публикации HEAD совпадал с актуальной интеграционной основой; незакоммичены были только тест и Ledger, диагностические записи сохранены |
| `dotnet restore MinecraftLauncher.sln` | Успешно с разрешённым доступом; первоначальная попытка в песочнице не смогла прочитать пользовательский NuGet.Config |
| `dotnet build MinecraftLauncher.sln --no-restore` | Успешно; 0 warnings, 0 errors |
| Отдельный `CaptionButtonsExecuteTheirWindowActions`, `--no-build --no-restore` | 1/1 passed; реальное окно на этом компьютере при DPI 150% |
| `dotnet test MinecraftLauncher.sln --no-build --no-restore` | Core 241/241, App 11/11; всего 252 passed, 0 failed, 0 skipped |
| `dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore` | Успешно, exit 0; форматирование не меняло файлы |
| Multi-DPI и midpoint | Скомпилированный helper проверен на изолированном WPF layout с SetRootDpi: 100/125/150/175/200%, дополнительно X/Y 125/175% и 175/125%. Обе оси и midpoint-to-even соответствуют WPF |
| Контроль регрессий helper | Отклонены неправильные Width/Height, отключённый rounding и отсутствие layout (нулевые Actual-размеры) |
| Markdown/ссылки/`git diff --check`/scope | На этапе исправления проверены структура Ledger, внутренние файловые ссылки и diff; изменения ограничивались `WindowChromeBehaviorTests.cs` и этим Ledger |

Полный оконный сценарий теста выполнялся при фактическом DPI 150%; другие масштабы
проверены на изолированном layout, а не на физических мониторах. Проверки helper
выполнялись диагностическими командами в памяти, новых тест-кейсов/файлов не добавлено.
Версия, зависимости, MainWindow.xaml и UseLayoutRounding приложения не менялись.
После этого исторического локального этапа commit/push и создание Draft PR #9 были выполнены;
на этапе публикации merge не выполнялся (последующий merge PR #9 подтверждён выше).
GitHub checks в том снимке отсутствовали, поэтому 252/252 —
результат предыдущего локального прогона, а не подтверждение CI.

Историческая документационная проверка до merge PR #9, 10.10.2026:

| Проверка | Результат |
| --- | --- |
| Git/fetch | До изменений рабочая ветка `fix/v061-windowchrome-dpi` была чистой и совпадала с `origin/fix/v061-windowchrome-dpi` на `e7aafe7`; `origin/main` и `origin/feat/v0.6.1` подтверждены fetch |
| GitHub | PR #8 merged в `feat/v0.6.1`; PR #9 открыт как Draft, не merged; check runs/statuses/workflow runs для опубликованного head отсутствуют |
| Scope | Документационное исправление ограничено `AGENTS.md` и `docs/PROJECT_LEDGER.md`; `WindowChromeBehaviorTests.cs` не изменяется |
| Тесты | Не запускались: код не менялся; 252/252 относятся к предыдущему локальному прогону исправления WPF/DPI-теста |
| Документация | Markdown-структура и относительные ссылки корректны; проверка секретов/приватных адресов и `git diff --check` прошла; diff содержит только `AGENTS.md` и Ledger |

Проверки программного Storage Root этапа, 11.10.2026:

| Проверка | Результат |
| --- | --- |
| Git/fetch/baseline | Чистое исходное дерево; новый feature HEAD и origin/feat/v0.6.1 — `52ee0d8`. PR #10 остаётся Draft, его код/коммиты не включены; main/release неизменны |
| Restore | Успешно; sandbox не прочитал пользовательский NuGet.Config, разрешённый повтор прошёл. Local tool restore подтвердил vpk 1.2.158 без обновления manifest |
| Build | `dotnet build MinecraftLauncher.sln --no-restore`: 0 warnings, 0 errors |
| Полный новый test run | Core 288/288 + App 11/11 = **299/299**, 0 failed/skipped; 47 новых cases относительно baseline 252. Это не прогон 280 cases PR #10 |
| Целевые проверки | Итоговый отдельный прогон всех 47 новых root/guardian/PackSync cases — 47/47. Первоначальный промежуточный целевой прогон после исправления path normalization/junction command — 38/38 |
| Диагностика тестов/format | Сначала выявлены normalization trailing-dot alias и неверная quoting junction command; исправлены. ACL recovery test потребовал явной записи восстановленного DACL. Новые LF приведены к принятому CRLF; `format --verify-no-changes --no-restore` затем прошёл |
| Реальные границы проверки | Временные каталоги на текущем диске, фактически отсутствующая буква тома, Windows ACL/junctions, отдельный PowerShell lock owner и тестовые child processes вместо Minecraft. Реальная установка на D:/почти полный C:, GUI/MSI/packaging/installed update не выполнялись |
| Scope | Новые resolver/policy/error/tests; минимальные bootstrap/paths/guardian/consumer изменения и architecture/game-launch/releasing/Ledger. UI XAML/ViewModels, RAM, версии, dependencies, CI/scripts, v1 server/pack contracts и historical docs не меняются |
| Документация/чистота | Проверены четыре Markdown-документа и 35 внутренних файловых ссылок, UTF-8/fences/таблицы/заголовки, секреты в diff и всех новых файлах. Раздел Pack Manager сохранён; `git diff --check` чистый, staged changes отсутствуют |

Прошлые успешные 280 тестов и пользовательский Minecraft smoke принадлежат PR #10/его
истории, не этому новому прогону. Первая ветка не содержит одиночный режим; объединение
двух feature-веток и full regression review — отдельный следующий этап.

### Исторический остановленный прототип SR-001/SR-002/SR-003, 11.10.2026

**BLOCKED — это рабочий прототип, не безопасная готовая реализация. Не публиковать и не
использовать его для реального Minecraft.** `PROC_THREAD_ATTRIBUTE_JOB_LIST` атомарно
назначает процесс в Job, но не передаёт ему собственный handle Job Object. После аварии
последнего владельца handle kernel object может ещё существовать, однако его имя исчезает.
Это подтверждено отдельными crash processes и
[Microsoft: Life Cycle of an Object](https://learn.microsoft.com/en-us/windows-hardware/drivers/kernel/life-cycle-of-an-object).
В текущем `WindowsGameJob.GetActiveProcesses()` ERROR_FILE_NOT_FOUND ошибочно трактуется
как zero processes; cleanup pending intent в `ProfileGameActivity` поэтому не доказан.
При записанном PID противоречие обнаруживается fail-closed, но это не закрывает pre-PID gap.
До уточнения контракта никакой обходной реализации не внесено.

| Новая проверка текущего прототипа | Фактический результат |
| --- | --- |
| Git/fetch/PR #10 | Fetch с разрешённым доступом успешен; HEAD/origin integration `52ee0d8`, main `bd98ecd`; PR #10 Open Draft, не merged, не включён |
| Restore | Успешен с разрешённым доступом к NuGet.Config/cache; зависимости не обновлены |
| Build после добавления tests/host | 0 errors, 0 warnings; первоначальные CA1416 и xUnit2031 исправлены до последней сборки |
| Существующие Guardian tests | 18/18 passed на промежуточном прототипе до добавления crash tests |
| Новые Crash + Safety tests | 13 passed, 11 failed, 0 skipped, всего 24: шесть crash cases и пять file-symlink cases упали. Symlink cases не дошли до проверок поведения из-за privileges |
| Повтор только Crash tests | 1 passed, 5 failed, 0 skipped, всего 6. Разница результатов отражает timing удаления имени Job/закрытия handles; гарантия нарушена, тесты не ослаблены |
| Повтор Symlink tests вне sandbox | 5/5 passed, 0 skipped; проверены existing/dangling settings/log links и прежний settings.json.tmp link, внешний sentinel не изменён |
| Full regression вне sandbox | Core 306 passed / 6 failed / 0 skipped (312); App 11/11 passed. Всего 317 passed / 6 failed / 0 skipped (323). Пять crash cases падают; дополнительно прежний ProfileFileManagerTests.GetOrCreateDirectory_RejectsInstanceReparsePoint_WhenLinksAreSupported ожидает InvalidOperationException, но Storage Root выбрасывает StorageRootException. Это дополнительный незакрытый contract/test mismatch; после stop он не исправлялся |
| Format verify | Exit 1: новые файлы имеют LF вместо принятого CRLF и обнаружены отступы ProfileGameActivity. После архитектурного stop код автоматически не форматировался |
| Diff-check/Markdown | Чистый; проверены 11 untracked файлов на whitespace/conflict markers и 37 внутренних ссылок четырёх docs, fences корректны. Staged changes отсутствуют |

Не завершены: независимые ACL/cross-session/nested-job проверки, отказ atomic assignment,
реальный mapped SMB volume, полный успешный regression,
реальная Java/CmlLib аргументация/streams smoke и оба режима после интеграции PR #10.
Настройки UI/RAM/версии/SDK/manifest/server protocol и production не менялись.

Для продолжения требуется согласовать сохранение handle Job Object в самом child
при атомарном создании (только минимальные query права и explicit HANDLE_LIST), либо
другой контракт устойчивого владения. Наследование handle — кандидат, не доказанное
исправление: нужно проверить сохранение handle JVM, дочерние процессы, ACL и lifetime.
Отсутствие имени не разрешено использовать как доказательство завершения неизвестной
сессии. Таймаут, обычный Process.Start и последовательное AssignProcessToJobObject
не являются допустимыми fallback. Постоянный privileged объект/служба не добавляются.

### Самостоятельный прототип уточнённого A — 11.10.2026

**Исторический этап до разрешения witness у descendants.** Четыре файла в
[tests/StorageRoot.JobPrototype](../tests/StorageRoot.JobPrototype/StorageRoot.JobPrototype.csproj)
не ссылаются на Launcher.Infrastructure и не меняют рабочий guardian. Owner, JVM и
повторный query выполняются отдельными процессами. Prototype owner создаёт unique
Global Job с per-user/SYSTEM DACL, flush/atomic versioned intent, QUERY-only inheritable
duplicate и вызывает CreateProcessW с двумя attributes одновременно. Нет KILL_ON_JOB_CLOSE,
breakaway, обычного Java Process.Start, последовательного Assign или timeout recovery.
Таймауты harness ограничивают ожидание теста, но не разрешают очистку неизвестного intent.

| Проверка именно standalone prototype | Результат |
| --- | --- |
| Git/fetch/PR #10 | Fetch с разрешённым доступом: integration/HEAD `52ee0d8`, main `bd98ecd`; PR #10 Open Draft, не merged, его код не включён |
| Runtime discovery | Найдены существующие managed Microsoft OpenJDK 25.0.1+8 и Oracle Java 1.8.0_51. Java 8 только опрошена через -version; crash protocol на ней не запускался после stop |
| Restore/build prototype | Успешны; последняя .NET prototype build — 0 warnings/errors. Это не новый build всей solution |
| Java diagnostic class | Javac 25 с --release 8 создал class в ignored artifacts; три предупреждения об устаревшем target 8. Зависимости/версии Launcher не менялись |
| Первый smoke | Диагностический CompareObjectHandles первоначально был привязан к kernel32 вместо документированного kernelbase; исправлен только prototype и повторён |
| Java 25 main, modern-main-02 | Query-only access 4 подтверждён через duplicate реального JVM handle, CompareObjectHandles и NtQueryObject. Owner принудительно завершён; новый dotnet query process открыл Job и сообщил active=2 при живой JVM. После release JVM завершилась с code 37, имя Job отсутствовало. Дополнительный active process не перечислен, точное объяснение active=2 не установлено |
| Arguments/environment/CWD/streams | В реальной Java подтверждены аргументы с пробелами/кавычкой/backslash, заданная environment variable, отдельный working directory, stdout и stderr. Дополнительное чтение stdout как Windows-1251 подтвердило Unicode-аргумент Привет; UTF-8 чтение harness отображало его некорректно. Это diagnostic decoding issue, не потеря аргумента. Harness не является готовой реализацией CmlLib streams |
| Java 25 ProcessBuilder, modern-spawn-01 | Родитель JVM PID 17696 и дочерняя JVM PID 5212 обе имели handle того же Job с access 4. Harness завершился exit 1 с FAIL: Java propagated the private witness handle to its subprocess. Cleanup завершил обе JVM через release files, не через KILL_ON_JOB_CLOSE |
| Проверка source | OpenJDK 25 Windows ProcessImpl создаёт child с bInheritHandles=TRUE и обычным STARTUPINFOW; родительский HANDLE_LIST не ограничивает этот следующий CreateProcess. Источник ниже |
| Integration/full test/format | Не выполнялись после отрицательного prototype gate. Предыдущие 317 passed / 6 failed и format exit 1 — исторический результат существующего рабочего guardian, а не новый прогон |

Исходник [OpenJDK 25 ProcessImpl](https://github.com/openjdk/jdk/blob/jdk-25-ga/src/java.base/windows/native/libjava/ProcessImpl_md.c)
подтверждает механизм дальнейшего наследования. Проверенный процесс — descendant
Java, не посторонний Launcher; escaping Job не продемонстрирован. QUERY-only handle
не предоставляет assign/terminate/set-information права. Возможность считать witness
разрешённым для всего guarded process tree требует явной политики владельца; самовольно
ослаблять условие отсутствия нежелательного наследования нельзя.

Не доказаны: запрет наследования всем unrelated/concurrent processes, исключение всех
canary handles, три crash phases на настоящей Java, nested/restrictive Jobs, cross-session
доступ, практическая ACL-denial/name-collision матрица, восстановление Launcher/root после
выхода JVM и всех descendants. Отсутствие имени Job по-прежнему не используется как
новое доказательство безопасной очистки в приложении: integration не сделана.

Новые файлы prototype и диагностический output сохранены для повторяемого review.
Build/class/output находятся в ignored bin/obj/artifacts и не являются кандидатами в commit.
SR-002/SR-003 и все ранее подготовленные изменения сохранены без перезаписи.
ProfileFileManager regression и format не исправлялись: эти действия владелец разрешил
после успешного prototype gate, который сейчас не пройден.

### Исторический recovery gate до согласования политики основной JVM — 11.10.2026

**BLOCKED по безопасному автоматическому recovery, не по наследованию внутри Job.**
Владелец разрешил QUERY witness дочерним процессам внутри того же Job; прежнее
сообщение harness о private-handle propagation больше не считается дефектом.
Первоначальная JVM сохраняет QUERY-only handle в проверенных сценариях. Запрещённые
kill/breakaway flags не установлены. Начальный CreateProcessW использует JOB_LIST и
explicit HANDLE_LIST со stdio + witness; полного job handle/canary в JVM нет.
После CreateProcess inheritable flag parent witness снимается; это не меняет handle
в JVM. Dedicated owner не создаёт другие процессы одновременно с этой короткой
inheritance window. Общая безопасность произвольного конкурентного Process.Start
в том же owner не заявляется.

| Новая проверка (не прогон всей solution) | Фактический результат |
| --- | --- |
| Git/GitHub | Fresh fetch: HEAD/origin integration `52ee0d8`, main `bd98ecd`, release `e426ecb`; PR #10 Open Draft, base integration, head `c604955`, не merged. Его два commits отсутствуют в HEAD; launch mode/UI/roadmap code не переносились |
| Самостоятельная .NET build | Prototype успешно собран, последняя build — 0 warnings/errors. Restore всей solution в этой сессии не повторялся; dependencies не менялись |
| Java compilation | Javac 25 с --release 8 для JobWitness: три obsolete-target предупреждения; отдельный HandlelessChild использует Java 25 FFM, без сторонних библиотек. Class files только в ignored artifacts |
| Java 25.0.1+8 и Java 1.8.0_51 | По три фазы: crash до native creation, после native creation до PID launch state, после PID state. До creation JVM не запускается; в остальных фазах JVM работает, новый независимый процесс открывает Job, реальный JVM handle имеет access 4; выход по release с code 37. Аргументы с пробелами/кавычкой/backslash/Unicode, environment, CWD, stdout/stderr подтверждены |
| Непосредственная post-create авария | Отдельный повтор на Java 8/25: owner завершён до ожидания JVM-ready; created.json с PID launch state отсутствует. Тестовый observer.json нужен только harness, не участвует в recovery и не заменяет production PID/state |
| ActiveProcesses=2 | Перечень JobObjectBasicProcessIdList + IsProcessInJob: Java и её conhost, не guardian. Например Java 25 PID 11464 + conhost 7676, Java 8 PID 12828 + conhost 21280; все в Windows session 17 |
| Descendant с witness | Java 25 ProcessBuilder: parent 15644, child 20732; Java 8: parent 12944, child 5068. После crash owner обе JVM внутри Job, handle child access 4. После parent exit37 child жив, independent query показывает 2 (child + conhost). После child release дерево завершилось, имя отсутствует |
| Descendant БЕЗ handles | Java 25 через native FFM CreateProcessW с bInheritHandles=FALSE, без breakaway: прямой child до parent exit подтверждён членом именно guarded Job и не имеет witness. В immediate crash case parent 17916, child 16820. После parent exit37 child жив и IsProcessInJob(NULL)=true, однако independent OpenJobObject возвращает missing. После child exit наблюдение имени всё ещё missing. Защита всего дерева без независимого держателя не гарантируется |
| Canary/неполный inheritance | Реальное сравнение kernel objects после DuplicateHandle: main JVM не получает inheritable canary file и full-access Job handle. Unrelated dotnet от того же owner после atomic launch (например PID 1664) не имеет witness и не состоит в guarded Job; проверен также independent-controller child. Произвольная конкурентная creation в том же handle table не тестировалась |
| ACL/коллизии | SDDL прочитан обратно: protected DACL, только текущий user + SYSTEM; initial flags0/active0. Повтор имени отклонён ERROR_ALREADY_EXISTS183. На изолированном Job explicit deny QUERY приводит к ERROR_ACCESS_DENIED5, не к missing/zero. Проверено под обычным пользователем и sandbox account; другой интерактивный session не проверялся |
| Nested/restrictive Jobs | Обычный внешний Job допускает вложенный guarded Java launch; crash owner не завершает JVM, code37 сохранён. Внешний ACTIVE_PROCESS_LIMIT2 допускает controller+guardian, но atomic native Java creation отклонена Win32 1816 именно в NativeJob.Start; intent есть, observer/PID/Java-ready отсутствуют, fallback не выполнен. Другие виды restrict/UI/security jobs не проверены |
| Диагностические промежуточные сбои | Java 8 ready.txt первоначально читался до закрытия записи; маркер заменён atomic move. Проба IsProcessInJob(NULL) первоначально передала null SafeHandle; исправлена IntPtr.Zero overload. Первоначальный quota1 тест блокировал создание guardian, не Java; заменён quota2 с проверкой места отказа. Эти исправления только в standalone harness |
| SR-002/SR-003 safety regression | Новый целевой прогон существующей Core test assembly: sandbox 13 passed/5 failed при создании symlink без privilege; разрешённый повтор — **18/18 passed, 0 failed/skipped**, внешние world sentinel неизменны. Нормализованные UNC/device, mixed local reopen и injected mapped-network DriveType проверены; настоящий SMB mapped drive не проверялся |
| Ограничения матрицы | Доступны только managed Java 8 и 25; Java 17/21 не найдены, не установлены и не проверены. Реальный Minecraft/CmlLib guardian pipe integration, другой interactive session, packaging/update и PR #10 Server/Singleplayer regression не выполнены |
| Integration/full regression/format | Уточнённый witness не интегрирован. Production source/test files в этом этапе не изменены; ProfileFileManager regression не исправлен. Новый полный solution restore/build/test/format не заявляется; исторические 317/323 и format exit1 остаются последним полным результатом |
| Чистота самостоятельного prototype | Исправлены только диагностические whitespace и CRLF; отдельный prototype format --verify-no-changes --no-restore прошёл (exit0), повторный prototype build 0 warnings/errors. Это не исправление format всей solution. Markdown: 38 внутренних файловых ссылок четырёх docs и fences корректны, git diff --check чистый; staged diff пуст |

**Recovery ambiguity:** новый независимый процесс после crash видит durable pre-PID
intent, завершённого guardian и ERROR_FILE_NOT_FOUND для Job. Это одинаково возможно
(a) при crash до создания Java и (b) после запуска Java, рождения child без handles,
выхода основной JVM и потери последнего witness. Во втором случае child может ещё
использовать instance. Даже после его выхода сохранённый intent/Job lookup не изменяются.
Observer PID и release files в тесте дают controller дополнительное знание; production
этого знания не имеет и не должен полагаться на них.

Допустимое ограничение whole-tree witness само по себе не считается потерей защиты
живой основной JVM. Но оно не доказывает безопасное освобождение root/profile для
Repair/Delete/reconfiguration. При отсутствующем Job состояние нельзя молча очищать
или считать ActiveProcesses=0. Fail-closed сохраняет данные, но без дополнительного
recovery evidence не обеспечивает автоматическое разблокирование. Проверка
root/profile recovery приложения поэтому **не пройдена**, а не заменена наблюдением
отсутствующего имени. Интеграция и обходная cleanup не выполнены.

Для продолжения нужно согласовать recovery policy для исчезнувшего Job/pre-PID
intent: дополнительное достоверное evidence/контракт либо явный отдельный recovery
с подтверждением отсутствия всех затрагивающих GameData процессов. Простое
ограничение гарантии живой основной JVM уже согласовано, но не разрешает опасную
очистку неопределённого дерева. Ни таймаут, ни перечисление всех java.exe без устойчивой
идентичности не предлагаются как доказательство. Сервис/новый keeper/GUI не добавлены.

### Согласованная политика основной JVM и интеграция — 11.10.2026

**Эта запись заменяет текущий BLOCKED-статус предыдущих исторических gates, но не
переписывает их результаты.** Владелец явно разрешил восстановление по отсутствующему
имени Job при подтверждённом witness-контракте и доказанном завершении guardian.
Защищаемый lifecycle — основная JVM Minecraft. Потомок без witness после её выхода
может оставаться в Job без доступного имени: это согласованное ограничение, не доказательство
завершения всего дерева. Такой потомок потенциально может продолжать использовать instance;
защита этого сценария отнесена к отдельному последующему направлению.

Реализованный контракт:

- Durable intent **schema 3, WitnessContractVersion 1**, уникальный token и Job name с SID;
  guardian PID/start time, затем основной PID/start time. Уникальный CreateNew temp,
  Flush(true) и same-volume atomic rename до CreateProcessW; неизвестные JSON-поля,
  версии и неполные identities отклоняются, JSON ограничен 16 KiB.
- Native CreateProcessW использует JOB_LIST и explicit HANDLE_LIST: три stdio handles
  плюс отдельный inherited **JOB_OBJECT_QUERY (4)** witness. Полный job handle не наследуется;
  parent witness закрывается сразу после native creation. Dedicated guardian не создаёт
  другие процессы одновременно в inheritance window. Нет kill/breakaway flags, обычного
  Process.Start fallback или последовательного AssignProcessToJobObject для Minecraft.
- IsActive/TryAcquireOwnership/recovery сериализованы exclusive `.game.lock`; root
  reconfiguration дополнительно требует exclusive Control lease. Живой guardian,
  удерживаемая ownership lock или существующий Job с ActiveProcesses>0 блокируют операции.
- При завершённом guardian существующий Job с ActiveProcesses=0 допускает recovery.
  Missing Job возвращается как **null**, не как 0: только v3/witness-v1 разрешает
  восстановление pre-PID intent по этому доказательству завершения основной JVM.
  Записанный живой/неопределённый основной PID при пустом/отсутствующем Job — противоречие,
  fail-closed. Timeout cleanup не используется.
- Legacy schema1 остаётся PID/start-time контрактом. Старый экспериментальный schema2
  без witness может восстанавливаться при отсутствующем Job только по отдельно записанному
  доказанно завершённому/reused PID; **pre-PID v2 missing Job сохраняется fail-closed**.
  AccessDenied, неизвестные версии/поля и неоднозначный guardian/main не трактуются как exit.
- SR-002: проверяется canonical path, включая UNC/device/mixed separators и DriveType.Network
  для mapped letter. SR-003: opened leaf проверяется через OPEN_REPARSE_POINT; уникальный
  settings temp CreateNew, лог пишет через append-only handle, внешние world sentinels сохранны.

| Новые проверки этого этапа | Фактический результат |
| --- | --- |
| Git/GitHub | Fresh fetch: HEAD/origin integration `52ee0d8d151e7e55327c20376a9678e78b3cc5c1`, main `bd98ecd`, release `e426ecb`; PR #10 Open Draft, head `c604955`, не merged. Ветка остаётся `feat/v061-storage-root`, staged пуст; PR #10 не включён |
| Java prerequisites | Официальные Temurin JRE 17.0.20.1+1 и 21.0.12.1+1-LTS скачаны только в ignored artifacts, SHA-256 сверены с официальным API. Java не установлена, app dependencies/version не менялись. SHA17 `bc21a93923103cdaac93ee337b0ae4365e739fde36df823dd456bc67c8a9d352`; SHA21 `d35f31e712f0fcf6ac5a093edc90204fbff22f720ba3950bd09d331d5e621636` |
| Standalone witness gate | **12/12**: Java 17/21 × java.exe/javaw.exe × intent/created/state. QUERY access4, canary/full-job exclusion, independent Job query, arguments/env/CWD/stdout/stderr/exit37 подтверждены. Immediate created crash выполняется до ожидания Java ready и до записи PID launch state. java.exe: main + conhost (ActiveProcesses2); javaw.exe: main (1) |
| Production guardian / реальные JVM | **16/16**, отдельный opt-in `Launcher.Guardian.TestHost verify-java`: Java 17/21 × java.exe/javaw.exe × три crash-фазы (12); Java 17/21 ProcessBuilder descendants (2); Java 25.0.1+8 direct handleless descendant × created/state (2). Только guardian принудительно завершается, Launcher lease закрыт, основная JVM продолжает работать и блокирует Play/Repair/Delete/root configuration. После JVM exit восстанавливаются profile и root |
| Missing Job / pre-PID | До JVM creation intent восстанавливается. В handleless-created фазе production PID state не записан; после основной JVM exit37 имя Job исчезает, descendant ещё жив, **recovery успешно выполнен именно при живом descendant**. Аналогичный state case прошёл. Это согласованная граница, не tree-lifetime гарантия |
| Целевые xUnit | Первоначальный targeted прогон **28/28**: crash6, recovery13, ProfileFileManager9. Затем добавлены четыре cases неполной guardian/чужой Job identity; все прошли в итоговом полном прогоне. Recovery: missing/empty/live-guardian, contradictory live main, unknown schema/witness/JSON, legacy v2 pre-PID fail-closed, реальный Job ACL AccessDenied5; четыре независимых Launcher-like процесса одновременно выполняют recovery через общий lock |
| ACL/nested повтор этого этапа | Standalone policy: protected user/SYSTEM DACL, collision183 отклонён, explicit QUERY deny5 не трактуется как missing/zero. Java17/21: normal nested Job проходит с живой JVM после crash и exit37; restricted outer Job ACTIVE_PROCESS_LIMIT2 отклоняет atomic Java creation ошибкой1816, без observer/PID/Java-ready и fallback. Проверенный Windows session17; иной interactive session не проверен |
| Restore | `dotnet restore MinecraftLauncher.sln` успешно; зависимости не обновлялись |
| Build | `dotnet build MinecraftLauncher.sln --no-restore`: **0 warnings, 0 errors** |
| Полный test | Итоговый `dotnet test MinecraftLauncher.sln --no-build`: **Core 329/329 + App 11/11 = 340/340**, 0 failed/skipped. Вне sandbox для реальных symlink/WPF проверок; 88 cases сверх интеграционного baseline252, не тесты PR #10. Предыдущий промежуточный полный прогон336/336 также успешен |
| Format | `dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore`: exit0. Исправлены отступы/CRLF вручную, autoformat всей solution не выполнялся |
| Standalone .NET prototype | Повторный build — 0 warnings/errors; отдельный format verify exit0. Прототип не входит в production packaging |
| Документация/Git | 38 внутренних файловых Markdown-ссылок проверены, fences сбалансированы; git diff --check чистый. HEAD не менялся, staged пуст; binary/runtime/diagnostic artifacts только ignored |

Для повторения opt-in JVM checks: собрать solution; с JDK скомпилировать
`tests/StorageRoot.JobPrototype/JobWitness.java` с `--release 8` и
`HandlelessChild.java` с JDK25 в отдельный ignored classes directory. Затем выполнить
`dotnet tests/Launcher.Guardian.TestHost/bin/Debug/net10.0/Launcher.Guardian.TestHost.dll
verify-java <полный-путь-java.exe-или-javaw.exe> <полный-путь-classes> <intent|created|state>
<main|spawn|handleless>` одной командой. Handleless требует Java25 FFM. Harness копирует
runtime в собственный временный GameData, не использует пользовательский Minecraft;
release файлы завершают тестовые JVM с code37. Эти 16 checks не входят в число xUnit340.

Ограничения проверки: это реальные JVM и production guardian, **не Minecraft/Fabric smoke**.
Подтверждены конкретные HotSpot-сборки Java17/21 и ранее Java8/25; не гарантируются произвольный
runtime/нативный мод, намеренно закрывающий witness. Cross-interactive-session/UAC account switching
не проверены; Global namespace и per-user DACL проверены в текущем Windows session.
Nested/restrictive Job, canary и ACL/collision standalone checks выше — отдельные результаты,
не автоматическое доказательство всех возможных корпоративных Jobs. Реальный SMB mapped drive,
installed/portable packaging/update E2E и Server/Singleplayer после PR #10 остаются следующими проверками.
Guardian crash по-прежнему сообщает Launcher ошибку наблюдения exit status; это не разрешает
конфликтующие операции: durable activity защита живой JVM сохраняется независимо от UI ожидания.
Закрытие Launcher/guardian не завершает Minecraft.

### Проверенный набор файлов — исторический снимок до публикации

Всего **39 файлов: 21 modified + 18 untracked** относительно HEAD `52ee0d8`.
Список включает накопленную реализацию Storage Root, не только последнее исправление.
Корень этой копии — `C:\Projetcs\minecraft-launcher`; на этапе review никакие файлы не staged.

| Каталог | Modified |
| --- | --- |
| docs | PROJECT_LEDGER.md, architecture.md, game-launch.md, releasing.md |
| src/Launcher.App | App.xaml.cs, Program.cs |
| src/Launcher.Infrastructure/Game | CmlLibGameLaunchService.cs, GameLaunchErrorMapper.cs, GameProcessGuardian.cs |
| src/Launcher.Infrastructure/Logging | FileAppLogger.cs |
| src/Launcher.Infrastructure/Pack | PackSyncService.cs |
| src/Launcher.Infrastructure/Persistence | JsonSettingsStore.cs, LauncherDataPaths.cs, ProfileFileManager.cs, ProfileGameActivity.cs, ProfileLockFiles.cs |
| src/Launcher.Infrastructure/Properties | AssemblyInfo.cs |
| tests/Launcher.Core.Tests | GameProcessGuardianTests.cs, Launcher.Core.Tests.csproj, PackSyncServiceTests.cs, ProfileFileManagerTests.cs |

| Каталог | Untracked (обязательно включать в review) |
| --- | --- |
| src/Launcher.Infrastructure/Game | AtomicJobProcess.cs, WindowsGameJob.cs |
| src/Launcher.Infrastructure/Persistence | SafeLeafFile.cs, StoragePathPolicy.cs, StorageRootException.cs, StorageRootResolver.cs |
| tests/Launcher.Core.Tests | StorageRootCrashTests.cs, StorageRootRecoveryTests.cs, StorageRootResolverTests.cs, StorageRootSafetyTests.cs |
| tests/Launcher.Guardian.TestHost | Launcher.Guardian.TestHost.csproj, Program.cs, RealJavaRecoveryChecks.cs |
| tests/StorageRoot.JobPrototype | StorageRoot.JobPrototype.csproj, NativeJob.cs, Program.cs, JobWitness.java, HandlelessChild.java |

## 13. Следующие действия и миграция документов

1. Независимое review Storage Root завершено: READY FOR COMMIT. Публикация одним commit,
   обычным push и Draft PR согласована; далее GitHub review опубликованного diff.
   Любой merge — только по отдельному разрешению. `main`/`release/v0.6.0` не менять,
   merge PR #10 автоматически не выполнять.
2. Следующий installer PR: отдельно согласовать SDK/vpk upgrade, GUI bootstrapper
   с `Setup --installto` либо MSI customization, correct per-user root locator и
   сохранение legacy/update pipeline. Packaging/first launch на D:, почти полный C:
   (включая temp/packages), existing v0.6.0 update и worlds/settings требуют реального E2E.
3. При последующей интеграции с PR #10 проверить пересечения Ledger/CmlLib/guardian,
   оба launch mode и полный набор тестов; в этой ветке singleplayer намеренно не добавлен.
4. UI/UX/RAM/ping — v0.6.2, полный Storage Manager/перенос — v0.6.3, несколько
   источников — v0.7.0; Pack Manager независимо. Версию менять только в release preparation.
5. Другие рабочие копии синхронизировать с повторной проверкой remote/branch/HEAD;
   исторические status/roadmap/handoff сохранять до отдельного согласования удаления.

Информация [PROJECT_STATUS.md](PROJECT_STATUS.md), [ROADMAP.md](ROADMAP.md) и
[AI_HANDOFF.md](AI_HANDOFF.md) перенесена по смыслу: release/state/storage/ограничения —
разделы 2–4/8–12; подробные version scope/gates/идеи — 5–7/10; старт сессии,
ограничения и отчётность — AGENTS и разделы 1/13. Исторические снимки остаются целиком,
с уведомлением о новом источнике. Старые файлы **не удалены**.

Предлагается удалить только эти три файла после подтверждения владельца:

| Кандидат | Покрытие переноса / последующее исправление ссылок |
| --- | --- |
| PROJECT_STATUS.md | State, release, storage, snapshot, требования, идеи и вопросы здесь; удалить/заменить ссылку на старый снимок в этом разделе |
| ROADMAP.md | Полные требования, тесты/gates, порядок работ и предложения перенесены; удалить/заменить ссылку на старый snapshot здесь |
| AI_HANDOFF.md | Workflow/безопасность/отчёт — AGENTS; state/scope/первый PR/вопросы — Ledger; удалить/заменить ссылку здесь |

README уже ведёт к Ledger/AGENTS. В сохраняемых исторических документах ещё есть
взаимные ссылки друг на друга; при согласованном удалении они исчезнут вместе с ними.
Перед удалением повторно найти все ссылки, включая новые документы, и заменить их
на Ledger/AGENTS. Специализированные документы и release notes удалять не предлагается.

## 14. Специализированные документы и карта кода

- [Архитектура](architecture.md), [запуск игры](game-launch.md).
- [Server Protocol v1](server-protocol.md), [Pack Manifest v1](pack-manifest.md).
- [Серверный Pack Manager](PACK_MANAGER_PLAN.md), [выпуск релизов](releasing.md).
- Release notes: [0.4.0-rc.1](release-notes/0.4.0-rc.1.md),
  [0.5.0-rc.1](release-notes/0.5.0-rc.1.md), [v0.6.0](release-notes/0.6.0.md).
- [Core](../src/Launcher.Core/Launcher.Core.csproj): модели/контракты/политики/координаторы.
- [Infrastructure](../src/Launcher.Infrastructure/Launcher.Infrastructure.csproj): HTTP,
  PackSync, persistence/locks, CmlLib/guardian, Velopack и Windows services.
- [App](../src/Launcher.App/Launcher.App.csproj): WPF, ViewModel, диалоги и composition root.
- [Core tests](../tests/Launcher.Core.Tests/Launcher.Core.Tests.csproj),
  [App tests](../tests/Launcher.App.Tests/Launcher.App.Tests.csproj),
  [release feed script tests](../tests/ReleaseFeedScriptTests.ps1).
- [CI](../.github/workflows/ci.yml), [manual packaging](../.github/workflows/package.yml),
  [release script](../scripts/build-release.ps1),
  [test-only update harness](../tools/Launcher.UpdateE2EHarness/Launcher.UpdateE2EHarness.csproj).
