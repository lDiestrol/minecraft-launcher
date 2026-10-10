# Единый журнал Minecraft Launcher

> Документационный снимок проверен 11 октября 2026 года, Asia/Yekaterinburg (UTC+05:00).
> Стабильная ветка: `main`; verified commit после `git fetch origin`:
> `bd98ecd721298981508d54466d11089c1647ab4f`.
> Стабильная версия — **v0.6.0**. Следующая программная разработка — **v0.6.1**.
> Предыдущий локальный полный прогон одиночного запуска: **280/280 проходят**;
> build — 0 ошибок/предупреждений, format verify — чисто; это не новый прогон.
> Владелец подтвердил пользовательский Minecraft smoke 10.10.2026: главное меню
> в Singleplayer и автоматическое подключение в Server; это не CI и не installed update E2E.
> Публичный снимок на 11.10.2026: [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8)
> и [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9) объединены в `feat/v0.6.1`.
> Проверенная интеграционная основа — `52ee0d8d151e7e55327c20376a9678e78b3cc5c1`.
> Реализация опубликована в [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10),
> `feat/v061-singleplayer-launch` → `feat/v0.6.1`; merge не выполнен.
> Владелец пересмотрел scope: v0.6.1 — запуск и установка/storage architecture;
> v0.6.2 — UI/UX, v0.6.3 — Storage Manager, v0.7.0 — мультисерверность.
> Здесь изменена только документация; installer не реализован, build/test/format не повторялись.

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
| Стабильная ветка | `main`, verified `origin/main` — `bd98ecd721298981508d54466d11089c1647ab4f`; текущий feature HEAD отслеживается отдельно |
| Интеграционная ветка | `feat/v0.6.1`, verified `origin/feat/v0.6.1` — `52ee0d8d151e7e55327c20376a9678e78b3cc5c1`; включает объединённые [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8) и [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9) |
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
также объединён в `feat/v0.6.1` с merge commit `52ee0d8`.
Feature-ветка `feat/v061-singleplayer-launch` создана от этой актуальной основы.
Актуальный head: [ветка на GitHub](https://github.com/lDiestrol/minecraft-launcher/tree/feat/v061-singleplayer-launch);
независимое review: [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10),
целевая ветка — `feat/v0.6.1`. До текущего документационного изменения проверены
чистое дерево и совпадение HEAD/origin/PR head на `3ae67ac` (первоначальный commit реализации).
SHA последующего документационного коммита следует смотреть в истории PR, не в Ledger.
Статусы PR и HEAD изменяемы; перед следующей работой их следует проверить заново.

Согласованная 10.10.2026 Git-стратегия: main сохраняет стабильную v0.6.0;
промежуточные feature/docs/fix PR объединяются в `feat/v0.6.1` только в рамках
разрешённой задачи. Полностью завершённая v0.6.1 позже переносится в main одним
Squash & Merge после явного согласования владельца. Объединение PR #9 подтверждено
чтением GitHub API. Для одиночного запуска владелец разрешил один commit, обычный
push feature-ветки и Draft PR; merge, tags и Release остаются запрещены.

## 3. Версии и отдельные направления

| Версия / этап | Состояние | Объём и подтверждение |
| --- | --- | --- |
| MVP-1 | Завершён | Shell/onboarding; исторический commit `664b6ed` |
| MVP-2 | Завершён | Minecraft/Fabric launch; старый branch snapshot `e77be5f` содержит 125 тест-кейсов |
| MVP-3 / pack sync | Уже реализован в стабильной основе | Pack Manifest v1, SHA-256 sync, staging, Repair; начинать этап заново нельзя |
| 0.4.0-rc.1 / 0.5.0-rc.1 | Исторические release notes | Сохранены в Git; публикация этих RC сейчас отдельно не проверялась |
| v0.5.0 | Завершён, исторический stable | Tag `v0.5.0`, commit `c0e3990`; основа проверенного ранее installed update |
| v0.6.0 | Текущий stable, опубликован | Dark Gaming UI, profile file management, locks/guardian, app updates и release tooling |
| v0.6.1 | Частично реализован, не выпущен | Server/Singleplayer и общий pipeline опубликованы в Draft PR #10; пользовательский smoke подтверждён владельцем. GUI installer, выбор диска для независимых App/GameData, адаптация путей и совместимость нового installed update ещё не реализованы |
| v0.6.2 | Scope пересмотрен владельцем, запланирован | Компактный адаптивный Dashboard, оконный/maximized UX, ScrollBar, карточки, точный RAM, Server List Ping и реальные players/slots, статусы/ошибки |
| v0.6.3 | Согласован, запланирован | Storage Manager: размеры, безопасное удаление и shared cleanup, перенос данных и отдельные каталоги профилей |
| v0.7.0 | Согласован, запланирован | Несколько серверных источников, source-aware profile identity, каталог/поиск/избранное, управление сборками и совместимое развитие news/feed |
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
к UI/UX v0.6.2 по пересмотренному scope. Начальная фраза ViewModel о ручной проверке
не описывает уже реализованный startup check — это известная UX-несогласованность.

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

Владелец официально пересмотрел приоритеты: два режима запуска одного профиля плюс
правильная графическая установка с выбором диска/каталога, первоначальным GameData
на том же диске и сохранением существующих данных/installed update v0.6.0.
Server Protocol v1 и Pack Manifest v1 остаются совместимыми; локальные пути задаёт
пользователь/Launcher, а не серверные JSON. Формат существующих settings должен читаться.

История решения: первоначальный scope v0.6.1 включал точный RAM и небольшие UX-правки,
а Storage Manager планировался как v0.6.2. Это прежний план, не текущая очередь:
Точный RAM и UX перенесены в v0.6.2, Storage Manager — в v0.6.3; новый scope v0.6.2
дополнительно включает редизайн Dashboard и реальные ping/player count.
Полноценный Storage Manager, произвольный перенос существующих установок и серверная
админка **не входят** в v0.6.1. Новая установка/storage architecture ниже — план, не готовая функция.

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

Реализовано 10.10.2026 в `feat/v061-singleplayer-launch`:
`GameLaunchMode.Server/Singleplayer`, общий `PlayAsync(mode)`, общие settings/instance
и подготовка. CmlLib получает endpoint только в `Server`; numeric default порта без
ServerIp не попадает в arguments. Tests проверяют фактические options и ProcessStartInfo
установленного CmlLib на legacy/QuickPlay metadata, sync/cancellation/exit и команды.
Обе кнопки помещаются в viewport 900×650 и доступны по Tab; synthetic layout проверен
при пяти DPI. Подробнее: [game-launch.md](game-launch.md), результаты — раздел 12.
Первоначальный агентский этап не запускал Java/Minecraft. Затем владелец подтвердил
ручным smoke 10.10.2026: «Одиночная игра» открывает обычное главное меню Minecraft,
«Играть» автоматически подключается к серверу. Это пользовательское свидетельство,
не новый агентский прогон и не CI; создание/сохранение мира, новый installer и installed
update этим не проверены. Реализация опубликована в Draft PR #10; merge и дальнейшая
программная разработка требуют отдельного разрешения.

### 5.2. v0.6.1 — Installer & Storage Architecture

Компактное ТЗ следующей сессии. Требования согласованы, конкретные механизмы ниже
предлагаются для design review и **ещё не реализованы**.

1. **Границы хранения.** App — отдельный каталог Velopack; GameData — вне App и
   его version/current/update-каталогов. Общие `versions`, `assets`, `libraries`,
   managed Java/`runtime` используются повторно; `instances/<profileId>` содержат
   `mods`, `config`, `saves`, `screenshots`, `resourcepacks`, `shaderpacks` и локальные
   данные профиля. Минимальные служебные настройки/указатель root отделены от больших
   файлов; размещение settings/logs и ограничения временных файлов нужно согласовать.
2. **Выбор тома.** GUI предлагает диск и каталог App; первоначальный GameData —
   соседний независимый каталог на том же выбранном томе (например, App и GameData
   под выбранной папкой на D:). До записи проверяются canonical path, права, место
   и reparse boundaries. Большие download/cache/staging не должны скрыто оставаться
   на C:. Минимальные OS/служебные записи допустимы; проверить также временные пакеты
   установщика/обновления. Наличие нужного GUI у закреплённого Velopack `1.2.158`
   **не подтверждено**: сначала проверить совместимый вариант, сохранив update pipeline.
3. **Доверенный root locator.** Предлагается небольшой versioned per-user файл
   конфигурации вне App, с абсолютным нормализованным GameData root, защищённый
   правами текущего пользователя и атомарно обновляемый на собственном томе.
   Точное место/формат и приоритет legacy detection — открытый вопрос. Указатель не
   берётся из server JSON или guardian request; недоступный/невалидный выбранный root
   даёт понятную ошибку, а не молчаливый новый instance на C:.
4. **Все потребители путей.** `LauncherDataPaths` уже принимает root в конструкторе,
   но App и отдельный guardian сейчас создают default paths. Общий доверенный resolver
   должен отработать в обоих процессах **до** проверки request path; поле RootDirectory
   в request не является источником доверия. Адаптировать CmlLib shared data и instance
   paths, Java, profile/shared locks, guardian requests/state, disk-space и path-safety
   проверки без обхода containment/reparse защиты или двух lock identities одного root.
5. **Staging и atomic replacement.** Pack staging находится на томе назначения,
   не в системном `%TEMP%` другого диска. То же правило для временного state/settings
   и atomic file replacement: source/target на одном томе. Гарантия остаётся пофайловой,
   не полной pack-транзакцией; crash/частичный I/O failure требуют безопасного recovery.
6. **Legacy/update safety.** При обновлении v0.6.0 сохранить прежний GameData root,
   profile identity, nickname/RAM/settings, миры и пользовательские файлы; новая схема
   установки не означает автоматический перенос старых данных. Existing installations
   должны продолжить работать, update заменяет только App. Учесть lifetime guardian,
   активную игру/блокировки и отказ установки; не терять действующий root при сбое.
   Произвольные переносы — v0.6.3. Update/uninstall не должны попутно удалять worlds.
7. **Проверки.** Чистая установка на D: и первый реальный запуск обоих режимов;
   почти заполненный C: с достаточным местом на выбранном томе (проверить фактические
   download/temp/cache пути); несколько профилей одной версии с shared reuse и
   изоляцией instance; повторный запуск после рестарта без повторного скачивания
   неизменных файлов. Installed v0.6.0 → v0.6.1: сохранность settings/profiles/worlds,
   корректный restart/guardian/update и отсутствие записи Minecraft внутрь App.
   Добавить unit/integration cases для root persistence/legacy precedence, недоступного
   диска, permissions/reparse, locks и I/O failure; packaging/installed E2E — отдельные
   реальные проверки, пользовательский smoke 10.10 их не заменяет.
8. **Решения и зависимости.** Сначала compatibility study GUI/Velopack, затем
   согласование root/legacy/settings контракта, затем адаптация всех потребителей и
   тестов, installer integration, clean-install/installed-update E2E. Открыты технология
   GUI, per-user/all-users/ACL, temporary package location, legacy discovery и fallback,
   portable behavior, несколько установок, offline/read-only volume и смена буквы диска.
   Не менять зависимости или installer scripts до отдельного разрешения на реализацию.

### 5.3. Порядок и gate v0.6.1

Независимое review PR #10 и merge только по разрешению владельца; installer — отдельная
согласованная feature-задача от актуальной `feat/v0.6.1`, не продолжение кода в этом PR.
Первый технический шаг — read-only проверка возможностей закреплённого Velopack/vpk
и совместимых GUI-вариантов: выбор App/GameData тома, upgrade detection, package/temp
paths и сохранение существующего update pipeline. Результат — матрица совместимости
и согласованный контракт bootstrap/root resolver до программирования.
Release gate: build/tests/format, реальные clean first-launch, worlds/settings/restart,
release feed/packaging и installed update новой версии. Версии и зависимости в этой
документационной задаче не меняются; полная v0.6.1 ещё не готова к выпуску.

## 6. Последующие клиентские версии — пересмотренный scope

### 6.1. v0.6.2 — интерфейс и UX

Компактный адаптивный Dashboard, корректные оконный/maximized режимы, тёмные ScrollBar
и более компактные карточки профилей. Реальный Minecraft Server List Ping возвращает
ping, online players и максимальные slots; нужны таймауты, состояние «нет данных»
и отсутствие фиктивных показателей. Не ослаблять строгий Server Protocol v1 ради UI.
Улучшить статусы/ошибки, объяснения блокировок Play/Repair/Delete и отображение
реального channel `stable`/`dev` из package/runtime state; детали ошибок — в локальном логе.

Точный RAM в мегабайтах (сохранённые требования прежнего scope v0.6.1):

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

Дополнительно проверить compact/maximized layout, keyboard/focus/DPI и ScrollBar;
для Server List Ping — успешный ответ, offline/timeout, malformed response и отсутствие
данных без подмены нулями. Реальные параметры и timeout policy согласовать перед реализацией.

### 6.2. v0.6.3 — Storage Manager

История: нижеследующие ownership/cleanup требования ранее планировались как v0.6.2;
по решению владельца они перенесены в v0.6.3, а не отменены.

После завершения v0.6.1/v0.6.2 показать объём выбранного профиля и отдельно общих данных,
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

Дополнительно входят перенос **существующих** игровых данных и разные каталоги отдельных
профилей. Нужны согласованный план переноса/cancellation/recovery и проверка всех
root/lock identities; нельзя удалять shared-файлы, используемые другими профилями.
Первоначальный выбор диска в v0.6.1 не является готовым Storage Manager или переносом.

### 6.3. v0.7.0 — мультисерверность

Несколько независимых серверных источников конфигураций; profile identity учитывает
источник, чтобы одинаковые `profileId` не пересекали instances/settings/managed state/locks.
Каталог серверов, поиск, избранное, удобное переключение и управление несколькими
сборками. Миграция существующих identities и доверие к источникам требуют отдельного design.
Будущее расширение news/feed — только совместимым согласованным способом, без
нарушения строгого Server Protocol v1; новые поля нельзя просто добавлять в текущие JSON.

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
| Application/data roots v0.6.0 | Действующая default-схема: `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher` / `%LOCALAPPDATA%\MinecraftLauncher`; selectable roots v0.6.1 пока только предложены в разделе 5.2 |
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

Перед v0.6.1: независимое review PR #10, совместимый GUI installer и root/legacy contract
(раздел 5.2). Пользователь подтвердил main menu/server connect; создание/сохранение мира
и сохранность данных ещё требуют проверки. Реальные clean-install на выбранном диске,
packaging и installed update v0.6.0 → v0.6.1 не выполнены. CmlLib options/arguments
и compact 900×650/DPI ранее проверены автоматическими тестами.

Перед v0.6.2: точный RAM clamp/validation при безопасных min/max; реальные ping/player/slots,
timeout/no-data policy; достоверный Velopack channel в installed/portable/development;
компоновка и понятные сообщения блокировок Play/Repair/Delete.

Перед v0.6.3: состав установки профиля; пересечение managed config и личных настроек;
достоверный shared-file usage; preview; частичные ошибки cleanup/rollback; безопасный
перенос данных и отдельных instance roots без потери миров или обхода lock/ownership.

Перед v0.7.0: source-aware identity/миграция текущих профилей, доверие к нескольким
источникам, границы каталога/избранного и совместимый путь расширения строгих v1 схем.

Для Pack Manager: фактические public URL, NGINX roots/cache/invalidation,
ownership/permissions и текущий publishing source; backup/retention/rollback;
LXC OS/quota/storage/network/firewall/service credentials; VM lifecycle и health timeout;
client/server/common mod mapping; authoritative pack version, publisher/reviewer roles,
storage atomic primitives и требуемый audit export. Детали остаются в отдельном плане.

Прочие вопросы: portable update UX/E2E;
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
| 10.10.2026 | При подготовке одиночного запуска fetch/GitHub API подтвердили дальнейший merge [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9), интеграционный baseline `52ee0d8`. На старте дерево было чистым; создана `feat/v061-singleplayer-launch` от актуальной `origin/feat/v0.6.1`. Main/release не менялись |
| 10.10.2026 | По отдельному разрешению реализованы два режима одного профиля, общие launch pipeline/settings/instance и вторая кнопка. Добавлены проверки CmlLib options/arguments, обоих режимов sync/locks/cancel/exit/commands и compact/DPI/Tab. Restore/build/format успешны; Core 261/261 + App 19/19 = 280/280. На первоначальном локальном этапе commit/push/PR ещё не выполнялись; работа была остановлена для согласования публикации |
| 10.10.2026 | Затем владелец согласовал публикацию одним коммитом `feat: add singleplayer launch mode`, push `feat/v061-singleplayer-launch` и Draft PR в `feat/v0.6.1` для независимого review. Fetch повторно подтвердил baseline `52ee0d8`; проверены полный diff и три новых файла. Код и тесты на этом этапе не менялись, результаты 280/280/build/format относятся к предыдущему прогону. Состояние публикации определяется по ссылкам на ветку/PR в разделе 2; merge, tag/release и продолжение v0.6.1 не разрешены |
| 10.10.2026 | Одиночный запуск опубликован в [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10), `feat/v061-singleplayer-launch` → `feat/v0.6.1`; первоначальный commit реализации — `3ae67ac`. Владелец подтвердил отдельный ручной smoke: Singleplayer открывает обычное меню Minecraft, Server автоматически подключается. Это пользовательская проверка, не агентский запуск и не CI |
| 11.10.2026 | По новому согласованному заданию владельца актуализирован только Ledger: v0.6.1 — запуск + GUI installer/App/GameData, v0.6.2 — UI/UX/RAM/ping, v0.6.3 — Storage Manager, v0.7.0 — несколько источников. Прежний scope сохранён как история; добавлено предложенное Installer & Storage Architecture ТЗ. Документация публикуется одним согласованным commit и обычным push в существующий Draft PR #10; код, версии, зависимости, Pack Manager и исторические документы не меняются, реализация установщика не начинается |

SHA собственных документационных коммитов в Ledger не записываются;
их следует проверять по Git и истории PR.

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
на этапе публикации merge не выполнялся (последующее объединение отражено выше).
В том снимке GitHub checks для опубликованного head отсутствовали, поэтому 252/252 —
результат предыдущего локального прогона, а не подтверждение CI.

Историческая документационная проверка до merge PR #9, 10.10.2026:

| Проверка | Результат |
| --- | --- |
| Git/fetch | До изменений рабочая ветка `fix/v061-windowchrome-dpi` была чистой и совпадала с `origin/fix/v061-windowchrome-dpi` на `e7aafe7`; `origin/main` и `origin/feat/v0.6.1` подтверждены fetch |
| GitHub | PR #8 merged в `feat/v0.6.1`; PR #9 открыт как Draft, не merged; check runs/statuses/workflow runs для опубликованного head отсутствуют |
| Scope | Документационное исправление ограничено `AGENTS.md` и `docs/PROJECT_LEDGER.md`; `WindowChromeBehaviorTests.cs` не изменяется |
| Тесты | Не запускались: код не менялся; 252/252 относятся к предыдущему локальному прогону исправления WPF/DPI-теста |
| Документация | Markdown-структура и относительные ссылки корректны; проверка секретов/приватных адресов и `git diff --check` прошла; diff содержит только `AGENTS.md` и Ledger |

Предыдущие локальные проверки этапа реализации одиночного запуска, 10.10.2026:

| Проверка | Результат |
| --- | --- |
| Git/fetch/GitHub | На том локальном этапе fetch успешен с разрешённым доступом; `origin/feat/v0.6.1` и HEAD feature-ветки — `52ee0d8`; `origin/main` — `bd98ecd`, release-ветка — `e426ecb`. PR #8/#9 merged; до согласования публикации изменения оставались локальными |
| `dotnet restore MinecraftLauncher.sln` | Успешно с разрешённым доступом к пользовательскому NuGet.Config/cache; sandbox-попытка отказала в доступе |
| `dotnet build MinecraftLauncher.sln --no-restore` | 0 warnings, 0 errors |
| `dotnet test MinecraftLauncher.sln --no-build --no-restore` | Core 261/261, App 19/19; всего 280 passed, 0 failed, 0 skipped (28 новых cases относительно baseline) |
| Целевые проверки | CmlLib options и фактические legacy/QuickPlay process arguments; профиль/settings; sync-before-launch и failure; cancellation до launch; exit 0/7; IsBusy/Play/Singleplayer/Repair/Delete/update команды |
| Межпроцессная защита | Отдельный PowerShell-процесс удерживает profile lock: оба режима не доходят до sync, после выхода lock-owner запускаются. Дополнительно проверены два независимых file-lock provider, отмена и освобождение lease. Существующие guardian/shared-lock/SHA-256/disk-space/update tests проходят |
| WPF UI | Реальное окно 900×650 на текущем DPI 150%: кнопки видимы в viewport, привязаны к нужным командам, Focusable/Tab работают; caption-button DPI-aware размеры и действия сохранены |
| Multi-DPI | Реальный dashboard измерен в изолированном layout через SetRootDpi при 100/125/150/175/200%; native смена мониторов/DPI не выполнялась. Test fixture загружает production theme без production startup/network/settings writes |
| `dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore` | Успешно, exit 0 после нормализации CRLF новых файлов и удаления лишнего using; проверка не меняет файлы |
| Документация/scope | Обновлены только Ledger/game-launch; текст планов разделов 6–7 сохранён без изменений. Markdown/UTF-8/fences/таблицы корректны, 29 внутренних файловых ссылок проверены; `git diff --check` прошёл |
| Реальный smoke/packaging | Java/Minecraft не запускались, реальный серверный вход/main menu/world не проверены; installed update/release packaging E2E не выполнялись |

Новые тесты process arguments используют настоящий установленный CmlLib 4.0.6,
но синтетическую version metadata; они не доказывают совместимость реального pack
с одиночным миром. Существующие process/guardian tests запускают тестовые процессы,
не Minecraft. Полная v0.6.1 не считается готовой к выпуску по этому локальному прогону.

Исторические проверки этапа публикации для независимого review, 10.10.2026:

| Проверка | Результат |
| --- | --- |
| Git/baseline | Fetch успешен с разрешённым доступом; до публикационного commit HEAD и merge-base с `origin/feat/v0.6.1` совпадают с `52ee0d8`. Main/release не меняются |
| Полный diff/scope | Проверены 14 файлов, включая три новых; только согласованные Core/Infrastructure/UI, тесты и Ledger/game-launch. Бинарные артефакты, диагностические результаты, секреты и случайные файлы не добавляются |
| Код и предыдущие результаты | Код/тесты не менялись после предыдущего успешного прогона. Restore/build/280 тестов/format не повторялись; это не новый CI или E2E результат |
| Документация | Публикационные формулировки актуализированы; Markdown/UTF-8/fences/таблицы и 29 внутренних ссылок проверены, `git diff --check` прошёл. Планы v0.6.2/Pack Manager не менялись |
| Ограничения | Реальный Minecraft smoke, packaging и installed update E2E не выполнялись; перечислены в описании Draft PR. После публикации работа останавливается для независимого review |

Последующее пользовательское подтверждение, 10.10.2026: владелец вручную проверил
обычное главное меню Minecraft в Singleplayer и автоматическое серверное подключение
в Server. Источник — сообщение владельца, не CI и не запуск агента. Предыдущие строки
«не запускался» описывают именно исторический агентский этап; реальная проверка worlds,
нового installer/packaging и installed update v0.6.1 этим подтверждением не покрыта.

Документационная актуализация roadmap/installer plan, 11.10.2026:

| Проверка | Результат |
| --- | --- |
| Git/fetch/GitHub | Fetch успешен с разрешённым доступом; до изменения чистая `feat/v061-singleplayer-launch`, HEAD/origin/PR head `3ae67ac`; main `bd98ecd`, интеграционная основа `52ee0d8`, release `e426ecb`. PR #10 открыт Draft в `feat/v0.6.1`, не merged |
| Scope | Только `docs/PROJECT_LEDGER.md`; исходники, тесты, зависимости, версии, CI/scripts и исторические документы не меняются. Раздел Pack Manager сохранён без изменений |
| Документация | Проверены UTF-8, Markdown fences/заголовки/списки/таблицы и 27 внутренних файловых ссылок Ledger; diff содержит только Ledger, Pack Manager сохранён. В добавленном тексте не найдены secret/private-address patterns, `git diff --check` чистый; контроль diff/scope повторяется для коммита |
| Build/test/format | Не запускались для документационной правки. 280/280, build 0 warnings/errors и чистый format — предыдущие локальные результаты, не новый прогон и не CI |
| Ограничения | Installer architecture — предложение для следующей отдельно разрешённой сессии. Нового Minecraft/packaging/installed update E2E нет; merge/release/tag не выполняются |

## 13. Следующие действия и миграция документов

1. Выполнить независимое review [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10)
   в `feat/v0.6.1`; merge — только после отдельного разрешения. `main` и `release/v0.6.0`
   не менять. Эта задача заканчивается документацией, не реализацией установщика.
2. Следующую техническую сессию начать с read-only compatibility study закреплённого
   Velopack/vpk и GUI installer вариантов по разделу 5.3. До кода согласовать механизм
   выбора тома, root locator/legacy fallback, временные package paths и совместимость update.
3. После отдельного разрешения адаптировать пути App/CmlLib/guardian/locks/staging,
   добавить проверки clean D:/почти полный C:/shared reuse/повторный запуск и installed
   v0.6.0 → v0.6.1 update. Пользовательский main-menu/server smoke уже подтверждён,
   но сохранность worlds/settings и installer/update gate ещё требуют реального теста.
4. UI/UX, точный RAM и реальные ping/players — отдельный scope v0.6.2; Storage Manager
   и перенос — v0.6.3; мультисерверность — v0.7.0. Pack Manager вести независимо.
   Версию приложения менять только в согласованной release preparation.
5. Рабочие копии других машин синхронизировать с повторной проверкой remote/branch/HEAD;
   старые status/roadmap/handoff сохранять до отдельного согласования удаления.

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
