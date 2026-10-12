# Единый журнал Minecraft Launcher

> Review fixes PR #14, 12.10.2026 (UTC+05:00), та же `feat/v061-gui-bootstrapper`.
> NB-001…NB-005 исправлены без изменений игрового Launcher/Coordinator/guardian,
> StorageRootResolver и SDK161. Persistent ACL + проверенный pinned namespace,
> helper-owned read pins без дальнейшего наследования; отдельный shutdown и bounded
> GUI lifetime; результат последней подтверждённой попытки; адаптивный DPI layout;
> diagnostic-only immutable GUID/PID/start marker после Main, не Process.Start ack.
> Новый полный xUnit486/486 (20 новых), build0 errors/warnings, crash/recovery10/10;
> native177 production/179 diagnostic checks + Win32 GUI lifecycle component,
> format/diff/local links и изолированный production/diagnostic packaging чистые.
> Подробности и границы доказательств — [releasing.md](releasing.md).
> Публикация исправлений в существующий [Draft PR #14](https://github.com/lDiestrol/minecraft-launcher/pull/14)
> согласована; HEAD определяется Git/GitHub, не собственным SHA в этом журнале.
> Merge не выполняется, release-ready не объявляется. FAT/exFAT/cross-user/visual
> ordinary-session GUI→Setup E2E ещё обязательны; EnableLUA=0 не обходился.
> Ниже — исторические результаты первоначального PR и предыдущих этапов.

> Новый этап, 12.10.2026 (UTC+05:00): PR #13 merged, verified origin/feat/v0.6.1
> `9c4f5cfe31aecedd7d8a2eba603e1bcf2951f8a7`. От этой основы создана
> `feat/v061-gui-bootstrapper`; main/release/v0.6.0 не изменяются.
> Реализован Draft native GUI → self-contained helper → прежний Installation Coordinator
> → Setup161. Полные гарантии, IPC, результаты и ограничения — [releasing.md](releasing.md)
> и [architecture.md](architecture.md). Опубликован [Draft PR #14](https://github.com/lDiestrol/minecraft-launcher/pull/14)
> из `feat/v061-gui-bootstrapper` в `feat/v0.6.1`; merge не выполнялся.
> Новый полный прогон466/466, build0 errors/warnings; crash filter10/10, native48 checks;
> format/release-feed tests/изолированный packaging успешны. Эти числа не означают
> завершение Windows E2E: ordinary GUI smoke заблокирован host EnableLUA=0/elevated
> token, отказ до staging подтверждён, политика безопасности не менялась.
> Владелец согласовал manual smoke в обычном пользователе/VM после отчёта.
> Нет доказательства clean installation C/D, vendor TEMP trace, реального Launcher
> upgrade/shortcut/uninstall/crash E2E. Update fallback на C: остаётся нерешённым;
> managed staging cleanup не реализован, retry сохраняет isolated artifacts.
> Release-ready не объявляется. Дальнейший merge — только после независимого ревью
> и согласования владельца. Следующие строки — исторические снимки предыдущих этапов.

> GitHub-интеграция PR #10 и PR #11, 11.10.2026 (UTC+05:00).
> [PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10) объединён обычным merge:
> `cc30be0e1eb88262485aabb977d4e3f828e1ef84` в `feat/v0.6.1`.
> [PR #11](https://github.com/lDiestrol/minecraft-launcher/pull/11) адаптирован к этой основе
> без переписывания истории. Его статус/merge SHA и итоговый HEAD определяются по GitHub,
> [интеграционной ветке](https://github.com/lDiestrol/minecraft-launcher/tree/feat/v0.6.1) и Git.
> Эталон smoke сохранён: `test/v061-pr10-pr11-integration`, HEAD `a9f8642`.
> Production-код обоих режимов и intent3/witness1 должен совпадать с этим эталоном.
> Исторические отдельные прогоны: PR #10 **280/280**, PR #11 **340/340**;
> проверенная локальная интеграция **372/372**, JVM checks **16/16** — предыдущий этап.
> После адаптации выполнен новый полный прогон **372/372**, crash/recovery **23/23**;
> restore/build/format успешны, build 0 warnings/errors. Production/tests идентичны эталону.
> Владелец подтвердил реальный Minecraft/Fabric smoke на Windows11: legacy C: и custom
> NTFS D:, оба режима, сохранение мира после JVM restart, Repair без изменения level.dat,
> защита занятого профиля и освобождение после выхода; см. раздел 12.
> `main` сохраняет v0.6.0, SHA `bd98ecd721298981508d54466d11089c1647ab4f`.
> Исторический этап 11.10.2026: GUI Installer Architecture & Feasibility, без полноценной реализации.
> PR #11 merged; актуальный feat/v0.6.1 HEAD `6f5e11eced389416bb243804539b19f549cea5c5`.
> Исторический этап 11.10.2026: SDK и local vpk обновлены до 1.2.161 в
> [chore/v061-velopack-12161](https://github.com/lDiestrol/minecraft-launcher/tree/chore/v061-velopack-12161)
> от этой основы. Новый полный прогон 372/372, crash/recovery 23/23, build/format чистые.
> Владелец разрешил один commit, обычный push и Draft PR в feat/v0.6.1;
> фактическое состояние публикации — Git/GitHub этой ветки, не собственный SHA в Ledger.
> Merge/tag/Release запрещены; main/release, test-ветка и VHDX сохраняются.
> В том этапе Installation Coordinator и GUI не реализовывались.
> Исторический этап 11.10.2026: [PR #12](https://github.com/lDiestrol/minecraft-launcher/pull/12)
> merged; проверенный baseline feat/v0.6.1 — `c5b1f66c468685d92b90024cbba0f3744609012c`.
> Реализован managed Installation Coordinator в отдельной
> [feat/v061-install-coordinator](https://github.com/lDiestrol/minecraft-launcher/tree/feat/v061-install-coordinator).
> Новый полный прогон **418/418** (46 новых), build 0 warnings/errors, restore/format/diff чистые.
> Владелец разрешил logical commit/push/Draft PR в feat/v0.6.1; фактический HEAD/публикация
> определяются Git/GitHub этой ветки. GUI не создан; merge/tag/Release не выполняются.
> Актуальный этап 12.10.2026: исправления IC-001/IC-002/IC-003 в существующем
> [Draft PR #13](https://github.com/lDiestrol/minecraft-launcher/pull/13), та же feature-ветка
> и baseline c5b1f66. Новый full **441/441** (23 новых), build 0 errors/warnings;
> результаты и реальные границы vendor fault-injection — раздел 12 ниже.
> Владелец разрешил отдельный fix commit и обычный push для повторного review, не merge.

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
| Интеграционная ветка | `feat/v0.6.1`; текущий HEAD — [GitHub](https://github.com/lDiestrol/minecraft-launcher/tree/feat/v0.6.1)/Git. Основа адаптации Storage Root после merge PR #10 — `cc30be0e1eb88262485aabb977d4e3f828e1ef84`; прежний baseline PR #8/#9 — `52ee0d8` |
| Verified main | [bd98ecd721298981508d54466d11089c1647ab4f](https://github.com/lDiestrol/minecraft-launcher/commit/bd98ecd721298981508d54466d11089c1647ab4f) |
| Стабильный release | [v0.6.0](https://github.com/lDiestrol/minecraft-launcher/releases/tag/v0.6.0), GitHub API: `draft=false`, `prerelease=false`, опубликован 09.10.2026 |
| Release tag/target | [1ae3ae74b1b70d92031fe8706f80f85e506efd56](https://github.com/lDiestrol/minecraft-launcher/commit/1ae3ae74b1b70d92031fe8706f80f85e506efd56) |
| Версия исходников | `0.6.0` в [Directory.Build.props](../Directory.Build.props) |
| Runtime/packaging стабильной v0.6.0 | Self-contained `win-x64`, Velopack `1.2.158`, PackId `lDiestrol.MinecraftLauncher`, stable channel |
| Подпись | Authenticode отсутствует; пакеты unsigned |

Tag релиза и verified main различаются: после релизного commit в main добавлена
документация через PR #7. `git diff v0.6.0..origin/main` показывает только документацию.
Названия старых feature/release-веток не задают актуальную основу разработки.

Исторический снимок перед локальной интеграцией, 11.10.2026: `origin/main` — `bd98ecd`,
`origin/feat/v0.6.1` — `52ee0d8`,
`origin/release/v0.6.0` — `e426ecb`. [PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8)
объединён в `feat/v0.6.1` с merge commit `c31c982`; [PR #9](https://github.com/lDiestrol/minecraft-launcher/pull/9)
также объединён в `feat/v0.6.1` с merge commit `52ee0d8`.
Обе feature-ветки созданы независимо от этой основы.
Проверены опубликованные heads: PR #10 `c604955d7ad484b165e3cbe25a61c94d4d198e70`,
PR #11 `c0a37e1964f195b53c9b6c7d8d2c9c86bbea89f6`.
На том этапе согласована только локальная test-интеграция; исходные PR оставались Draft.
Позже владелец подтвердил smoke и разрешил GitHub-интеграцию. PR #10 merged (`cc30be0`);
PR #11 содержит проверенные разрешения конфликтов/test adaptations и актуальную основу.
Статусы PR и HEAD изменяемы; перед следующей работой их следует проверить заново.

Согласованная 10.10.2026 Git-стратегия: main сохраняет стабильную v0.6.0;
промежуточные feature/docs/fix PR объединяются в `feat/v0.6.1` только в рамках
разрешённой задачи. Полностью завершённая v0.6.1 позже переносится в main одним
Squash & Merge после явного согласования владельца. Объединение PR #9 подтверждено
чтением GitHub API. Для одиночного запуска владелец разрешил один commit, обычный
push feature-ветки и Draft PR. Затем отдельно разрешены локальные merge-коммиты
в test-ветке, позже — обычный push PR #11 и GitHub merge PR #10/#11 после проверок.
Публикация test-ветки не требуется; tags/Release и изменение main/release запрещены.

## 3. Версии и отдельные направления

| Версия / этап | Состояние | Объём и подтверждение |
| --- | --- | --- |
| MVP-1 | Завершён | Shell/onboarding; исторический commit `664b6ed` |
| MVP-2 | Завершён | Minecraft/Fabric launch; старый branch snapshot `e77be5f` содержит 125 тест-кейсов |
| MVP-3 / pack sync | Уже реализован в стабильной основе | Pack Manifest v1, SHA-256 sync, staging, Repair; начинать этап заново нельзя |
| 0.4.0-rc.1 / 0.5.0-rc.1 | Исторические release notes | Сохранены в Git; публикация этих RC сейчас отдельно не проверялась |
| v0.5.0 | Завершён, исторический stable | Tag `v0.5.0`, commit `c0e3990`; основа проверенного ранее installed update |
| v0.6.0 | Текущий stable, опубликован | Dark Gaming UI, profile file management, locks/guardian, app updates и release tooling |
| v0.6.1 | Совместная реализация PR #10/#11, не выпущен | Server/Singleplayer + Storage Root/crash-safe guardian; пользовательский legacy/custom Minecraft smoke подтверждён. GUI installer и clean-install/installed update E2E остаются release gates |
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
update этим не проверены. Исторически реализация опубликована в Draft PR #10;
последующий согласованный merge PR #10/#11 и smoke описаны в разделе 12.

### 5.2. v0.6.1 — Installer & Storage Architecture

Компактное ТЗ следующей сессии. Требования согласованы, конкретные механизмы ниже
частично реализованы Storage Root этапом (раздел 5.4); GUI installer и его E2E **ещё не реализованы**.

1. **Границы хранения.** App — отдельный каталог Velopack; GameData — вне App и
   его version/current/update-каталогов. Общие `versions`, `assets`, `libraries`,
   managed Java/`runtime` используются повторно; `instances/<profileId>` содержат
   `mods`, `config`, `saves`, `screenshots`, `resourcepacks`, `shaderpacks` и локальные
   данные профиля. Минимальные служебные настройки/указатель root отделены от больших
   файлов; settings/logs пока находятся в GameData, package temp требует installer E2E.
2. **Выбор тома.** GUI предлагает диск и каталог App; первоначальный GameData —
   соседний независимый каталог на том же выбранном томе (например, App и GameData
   под выбранной папкой на D:). До записи проверяются canonical path, права, место
   и reparse boundaries. Большие download/cache/staging не должны скрыто оставаться
   на C:. Минимальные OS/служебные записи допустимы; проверить также временные пакеты
   установщика/обновления. Наличие нужного GUI у закреплённого Velopack `1.2.158`
   **не подтверждено**: сначала проверить совместимый вариант, сохранив update pipeline.
3. **Доверенный root locator.** Реализован небольшой versioned per-user файл
   конфигурации вне App, с абсолютным нормализованным GameData root, защищённый
   правами текущего пользователя и атомарно обновляемый на собственном томе.
   Locator schema1 в MinecraftLauncherControl, legacy при отсутствии указателя
   (раздел 5.4). Указатель не
   берётся из server JSON или guardian request; недоступный/невалидный выбранный root
   даёт понятную ошибку, а не молчаливый новый instance на C:.
4. **Все потребители путей.** `LauncherDataPaths` уже принимает root в конструкторе,
   и App/guardian используют общий trusted resolver **до** проверки request path;
   поле RootDirectory в request не является источником доверия. CmlLib shared/instance
   paths, Java, locks, guardian requests/state и disk-space используют один root
   с сохранением containment/reparse проверок и lock identity.
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
   GUI, all-users/UAC identity, temporary package location и installed/portable E2E.
   Legacy/no-fallback/multiple-apps/недоступный том определены в разделе 5.4;
   произвольная смена диска/перенос данных не реализованы.
   Не менять зависимости или installer scripts до отдельного разрешения на реализацию.

### 5.3. Порядок и gate v0.6.1

Независимое review PR #10 и merge только по разрешению владельца; installer — отдельная
согласованная feature-задача от актуальной `feat/v0.6.1`, не продолжение кода в этом PR.
Исследование pinned Velopack/vpk выполнено в Storage Root этапе: CLI --installto и MSI
подтверждены, готовый GUI picker не подтверждён (см. [releasing.md](releasing.md)).
Root resolver реализован, GUI-механизм и installer/update E2E требуют отдельного согласования.
Release gate: build/tests/format, реальные clean first-launch, worlds/settings/restart,
release feed/packaging и installed update новой версии. Версии и зависимости в этой
интеграционной задаче не меняются; полная v0.6.1 ещё не готова к выпуску.

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
Пользовательский Minecraft smoke legacy C:/custom NTFS D: подтверждён (раздел 12).
Custom-directory install/update E2E настоящего Launcher ещё не выполнялись;
диагностические host проверки описаны отдельно в разделе 12.
GUI-путь выбран в architecture study; последующий Coordinator связывает initial root
с установочным lease до первого запуска. Native GUI/helper packaging ещё предстоит.
Pinned Velopack capabilities/deprecation
и предложение отдельного upgrade approval: [releasing.md](releasing.md).

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
| Concurrency | Profile operation/shared install locks; Control session lease; durable intent3/witness1 защищает основную JVM после guardian crash. Active Job блокирует recovery; handleless descendants после main exit исключены |
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

Перед v0.6.1: PR #10/#11/#12 уже merged; остаются независимый review Coordinator,
совместимый GUI installer и реализация согласованного root/legacy contract в нём
(раздел 5.2). Владелец подтвердил оба режима, сохранение мира/restart и Repair в legacy/custom root.
Реальные clean-install на выбранном диске,
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

12.10.2026: по независимому review PR #14 устранены NB-001(P1), NB-002…NB-005(P2)
и устаревшее описание GUI/IPC/helper в architecture. Проверяется реальный owner/DACL
каждого родителя, volume persistent ACL, protected private root и no-reparse handles;
directory pin содержит FILE_LIST_DIRECTORY (metadata-only не запрещал rename —
это воспроизведено и исправлено). Payload/dependency hashes проверяются повторно,
read pins переданы helper; перед Setup/launch он снимает дальнейшую inheritable flag.
Неподходящий том/ACL отвергается без extraction/helper и fallback; чужие ACL не меняются.
Shutdown reader отделён от cancel, helper wait1s и native-only watchdog5s; ни Setup,
ни Minecraft не убиваются GUI, intent/locator не очищаются. Worker завершается до
уничтожения app; retry/queued completion не вызывает длительный join или stale exit.
Layout100/125/150/200%, footer/work-area/scrollable paths/errors проверены расчётами.
Diagnostic Main публикует отдельный immutable `launch-<GUID>.marker.json` schema1
с PID/start UTC ticks; harness сопоставляет с trusted creation evidence до30s.
Обнаруженная гонка замены общего marker при открытом reader исключена отказом от
общего заменяемого файла. Старые confirmations/unknown schema/PID/time не дают успех.
Новые20 xUnit cases:14 marker +6 pin metadata/lifetime. Full486/486; native177/179
и настоящий Win32 component WM_CLOSE с hung child, сохранением intent sentinel,
release of parent pins/canary isolation и crash/recovery10/10. Component не запускает
установку и не является обходом identity checks или GUI→Setup E2E.
Изолированный diagnostic host с медленным Main и fresh repeat проверен отдельно;
полный clean/retry/reinstall в обычном Windows-пользователе ещё не выполнен.
FAT/exFAT rejection моделировался flags; ACL/rename/symlink/recheck/inheritance —
реальные Win32 API. Cross-user/visual/high-DPI/full installed update остаются gates.
SDK/версии/каналы/main/release/production/GameData/backup/VHDX не изменялись.
Согласован отдельный fix commit и обычный push в тот же Draft PR, без merge.

12.10.2026: native toolchain подготовлен по разрешению владельца из официального
Microsoft installer: Build Tools2022 17.14.41, MSVC19.44.35229, linker14.44.35229,
SDK/RC10.0.26100.0; C: до установки свободно417852006400 bytes, exit0 без reboot.
Minimal /MT Win32 probe подтверждает UTF16/asInvoker; .NET SDK/Velopack не обновлялись.
Fetch успешен, baseline PR13 подтверждён; новая отдельная GUI-ветка.
Добавлены helper/protocol/catalog/native extraction/state machine, packaging wrapper,
25 xUnit cases, native48 checks и developer-only diagnostic E2E harness.
Изменение backend ограничено internal compiled product binding для отдельного
diagnostic PackId; default product, Server/Singleplayer, Storage Root/Java guardian
не меняются. Restore/build/full466/466/format/release-feed/package проверены заново.
Прямой GUI и обычный Explorer invocation отказали с elevated-token до любых
install writes; подтверждён EnableLUA=0. Никакие Windows security settings не менялись.
Владелец согласовал внешний ordinary-session smoke; его результаты пока отсутствуют.
Опубликован [Draft PR #14](https://github.com/lDiestrol/minecraft-launcher/pull/14)
в `feat/v0.6.1` для независимого ревью с незакрытым E2E и fallback release gate,
не для автоматического merge/релиза. Author Ivan/noreply согласован только для команд
этого этапа, без изменения Git config. Полная проверка данных на D/VHDX не заявляется.

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

| 11.10.2026 | Fetch подтвердил последующее объединение PR #9: актуальная интеграционная основа `52ee0d8`, main `bd98ecd`, release `e426ecb`. PR #10 открыт Draft, head `c604955`, не merged. Из чистой `feat/v061-singleplayer-launch` создана независимая `feat/v061-storage-root` от origin/feat/v0.6.1 без переноса коммитов PR #10 |
| 11.10.2026 | По разрешению реализован локальный Storage Root: versioned pointer, fail-closed resolver/session для App/guardian, legacy preservation, initial placement API, lease contention, path/reparse checks и development dotnet host. Новые filesystem/guardian/CmlLib/PackSync tests. UI/launch modes/RAM/protocol/dependencies не менялись. Commit/push/PR/merge/tag/release не выполнялись |
| 11.10.2026 | Read-only исследование pinned Setup/vpk/MSI 1.2.158 и NuGet CriticalBugs deprecation: --installto/MSI подтверждены, штатный GUI folder picker не подтверждён. Связанная OS architecture regression исправлена 1.2.161; upgrade SDK/vpk предложен отдельно, но не выполнен. Installer не создан |
| 11.10.2026 | Владелец согласовал вариант A для SR-001. Добавлен локальный прототип durable intent v2 и атомарного CreateProcessW с JOB_LIST/HANDLE_LIST, без KILL_ON_JOB_CLOSE, breakaway или fallback. Отдельный test-only guardian принудительно завершается в трёх фазах. Crash tests выявили принципиальное ограничение: имя temporary Job Object исчезает при закрытии последнего handle, хотя child остаётся жив. Отсутствие имени не доказывает отсутствие игры; до записи PID прототип может ошибочно очистить intent. Реализация остановлена до обходных изменений, SR-001 P1 остаётся открыт |
| 11.10.2026 | Локально добавлены canonical drive/UNC/device проверки SR-002 и защита settings/log leaf SR-003 через OPEN_REPARSE_POINT с проверкой opened handle; settings использует unique CreateNew temp. Network classification unit test использует injected DriveType, реальный SMB mapped drive не проверен. Пять symlink cases сначала не смогли создать ссылки в sandbox; повтор вне sandbox — 5/5 passed, внешние sentinel files сохранены. Commit/push/merge/tag/release, installer и интеграция PR #10 не выполнялись |
| 11.10.2026 | Владелец согласовал сначала самостоятельный прототип уточнённого A: query-only witness через HANDLE_LIST одновременно с JOB_LIST. Создан отдельный tests/StorageRoot.JobPrototype без зависимостей на Launcher и без включения в solution/packaging. Реальная managed Java 25.0.1 сохранила handle с access 4 и пережила аварийное завершение owner; независимый query process открыл Job. Обычный Java ProcessBuilder передал этот же private handle дочерней Java с access 4. По условию владельца реализация остановлена до интеграции и обходных изменений. Наследование в descendant само по себе не доказывает выход из Job или нарушение сохранности данных; строгая граница «только исходная Java» не подтверждена |
| 11.10.2026 | Затем владелец явно разрешил QUERY witness в descendants того же Job. Самостоятельный prototype расширен: реальные Java 8/25 в трёх crash-фазах, immediate crash до JVM-ready, список PID/членство/права/flags, canary и unrelated process от того же owner, наследующий descendant и direct handleless child (Java 25 FFM), nested/restrictive Job, DACL denial и name collision. Живая основная JVM не потеряла witness. Handleless descendant остался жив внутри Job после JVM exit, но имя исчезло. Это допустимо отмечаемая граница защиты дерева, а не повтор прежнего запрета наследования; безопасная автоматическая очистка pre-PID intent не доказана. Уточнённый witness не интегрирован, production diff и SR-002/SR-003 сохранены. Подробные новые результаты ниже |
| 11.10.2026 | После предыдущего stop владелец явно согласовал recovery при отсутствующем Job и завершённом guardian для подтверждённого witness-контракта основной JVM. Проверены Java 17/21, интегрированы intent v3/witness v1 и атомарный запуск со stdio + QUERY witness. Реальные production-guardian crash/recovery checks прошли; legacy/unknown intents и AccessDenied fail-closed. Сохранены SR-002/SR-003, исправлен точный exception contract ProfileFileManagerTests без ослабления containment/reparse проверок; новые результаты 340/340 ниже. Никакой публикации или интеграции PR #10 |
| 11.10.2026 | После публикации PR #11 владелец согласовал локальную совместную интеграцию PR #10 → PR #11 в отдельной test-ветке от 52ee0d8. Три конфликта разрешены по смыслу: оба режима + trusted root/shared mapping; история Storage Root review и roadmap сохранены. На том этапе оригинальные PR не менялись, GitHub merge не выполнялся |
| 11.10.2026 | Владелец подтвердил реальный Windows11 smoke на legacy C: и custom NTFS D: через ConfigureNewRoot, оба режима, Fabric/Java/mods на D:, сохранение мира StorageRoot-Test после полного JVM restart, Repair с неизменным SHA-256 level.dat, конфликтующие Launcher и recovery. Затем разрешил commit/push/merge PR #10/#11; PR #10 объединён обычным merge cc30be0. PR #11 адаптируется через сохранённую эталонную test-ветку с проверкой идентичности production-кода и новым regression; installer не начинается |
| 11.10.2026 | После merge PR #10/#11 и отдельного architecture study владелец разрешил SDK/vpk upgrade 1.2.158 → 1.2.161. Создана chore/v061-velopack-12161 от origin/feat/v0.6.1 6f5e11e; сохранены прежние локальные Ledger/releasing исследования. Обновлены только три Velopack PackageReference и local vpk pin, текущая документация. Новые restore/build/372 tests/format, crash/recovery23, release-feed script и packaging успешны; isolated mixed-SDK installed update и fresh Setup161 подтверждены. Один commit/push/Draft PR разрешены, merge/release и installer implementation — нет |
| 11.10.2026 | После объединения PR #12 владелец согласовал Installation Coordinator без GUI. Fetch/API подтвердили c5b1f66; из чистой chore/v061-velopack-12161 создана feat/v061-install-coordinator от origin/feat/v0.6.1. Реализованы API/preflight, exclusive storage lease + durable installation intent, atomic installer Job/witness, trusted payload и installed-layout verification, startup gate, legacy preservation и безопасный retry без rollback. Новый полный regression418/418, crash phases и изолированный настоящий Setup161 подтверждены; данные/версии/зависимости/оба game modes не менялись. Публикация feature Draft PR разрешена для независимого review; GUI/merge/release запрещены |

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

Исторически PR #11 опубликован как Draft в feat/v0.6.1; его HEAD подтверждён GitHub
при начале интеграции. Последующий merge проверен перед SDK upgrade.

## 12. Последние проверки

### Исправления Installation Coordinator review, 12.10.2026

Работа в существующей feat/v061-install-coordinator от исходного PR #13 HEAD94f1971,
дерево было чистым. Fetch проверил main bd98ecd, integration c5b1f66 и неизменный head
PR #13; PR open/Draft. Исправления включены в отдельный logical fix commit этого PR,
не переписывают историю. Актуальный SHA/status проверяются Git/GitHub, не дублируются здесь.

| Замечание / проверка | Новый фактический результат |
| --- | --- |
| IC-001 P1 | Разделены IsAppVerified, IsRegistrationVerified и completion. HKCU проверяет точный PackId/App/version/title/authors/icon/Update uninstall commands/mandatory values; чужой registered App не заменяется. Только exit0 + inactive installer tree + files/root/HKCU публикуют отдельный atomic completion. Receipt никогда не доказывает успех; новая попытка не принимает старый completion token. Recovery сохраняет pending gate до Complete |
| IC-002 P1 | Receipt/intent либо receipt/completion должны точно совпадать. Связанный retry допускает обычные соседние элементы base и vendor rollback, не присваивает их и не удаляет. App.* без доказательства ничего не разрешает; reparse siblings запрещены. Fresh install и mismatched/missing receipt остаются fail-closed |
| IC-003 P2 | Canonical staging/GameData equal/ancestor/descendant отклоняются до intent/locator; recheck under lease и guard в Prepare. Нет миграции, смены locator или автоматического альтернативного каталога |
| Full xUnit | **441/441**, Core422 + WPF19, 0 failed/skipped. 23 новых cases в InstallationReviewRegressionTests, прежние418 сохранены |
| Targeted | **79/79**: InstallationSafety29, review regression23, installer crash4, StorageRoot crash6/recovery17. Owner crash при живом witness child блокирует recovery; после child exit gate остаётся до simulated successful retry publication |
| Отдельный crash повтор | **10/10**, installation4 + StorageRoot6, 0 failed/skipped, после итогового full |
| Restore/build | Новый restore успешен; build 0 errors/0 warnings. Первичный sandbox restore не прочитал user NuGet.Config, повтор вне sandbox успешен. Первичные CA1416 в новом registry reader устранены платформенной проверкой и foreach, без подавления diagnostics |
| Format/diff/Markdown | Verify-only format и diff-check успешны; CRLF согласно .editorconfig. Проверены17 Markdown documents, 88 внутренних файловых ссылок, fences/paths OK |
| Native Setup161 | Новый ignored diagnostic PackId MinecraftLauncher.InstallReview.20261012, dev0.0.1, win-x64/shortcutsNone. 3 реальные фазы через production installer Job: fresh post-extraction/pre-HKCU crash exit1223; retry post-rename/post-extraction crash exit1223; retry с no-delete handle backup, Setup exit0 при оставшемся rollback. Production registry verifier принимает реальную HKCU запись |
| Сохранность PoC | Diagnostic uninstall exit0, только его App/HKCU удалены. Тестовые backups/logs/world/locator сохранены ignored. SHA world EF64A0107C79505CB0567C7CA5F04147ED73F8E3207DEA5884B253AF0F08CF2B, locator 43F0058125C85BF399E4884766CEA016E3C9C74CACAA5CEBF6A41829982471B2 неизменны |

Существующие Minecraft/guardian/game Job/Server/Singleplayer/root resolver, UI/RAM,
версии, SDK/vpk pins и update source не менялись. Пользовательские GameData/VHDX/backups
и production не использовались. Preliminary targeted64/64 успешен вне sandbox; внутри
sandbox единственный fail был symlink privilege1314, не assertion. Позднее добавлены
ещё5 cases и выполнен указанный итоговый full.

Границы: native proof использует отдельный PackId и hook fixture, не E2E всей production
Coordinator/Launcher установки. Авария immediately-after-rename не воспроизведена: Job
завершён позже, в install hook. Actual registry-write ACL failure не инъецировался;
repeated failure/partial/wrong identity покрыты реальным FS с injected registry snapshot.
Crash между отдельными intent/receipt writes остаётся fail-closed; противоречивые metadata
требуют проверенной копии той же операции/отдельного обслуживания, не удаления gate.
Порядок описан в [releasing.md](releasing.md). Updater fallback, GUI, real Win10/D:/UAC/
low-space и stable installed E2E остаются следующими gates. Не объявлять release-ready.

### Installation Coordinator, 11.10.2026 (исторический этап до исправлений ревью)

Проверенная основа — merged PR #12, origin/feat/v0.6.1 c5b1f66. Main bd98ecd и
release/v0.6.0 e426ecb не менялись. Исходное дерево было чистым; ранее установленный
user GameData, VHDX, backups и test-интеграционная ветка сохранены.

| Проверка / контракт | Фактический результат |
| --- | --- |
| Scope | 16 файлов: 6 новых Installation classes, 3 существующих bootstrap/resolver/test-host entry files, 4 новых test files и 3 профильных документа. Нет GUI, изменения CmlLib/Server/Singleplayer/guardian JVM contract, версии/SDK pins, production или пользовательского root |
| API | Inspect/SuggestBaseDirectory/Preflight/InstallAsync, typed request/payload/result/progress/errors. Managed boundary для будущего out-of-process helper, без готового IPC/GUI. Preflight возвращает выбранные actual paths, staging и required space; короткие write probes не создают installation/locator |
| Paths / account | Existing StoragePathPolicy, UNC/device/network/reparse/overlap/system/temp checks; actual volume и write/space probes, ExpectedUserSid и elevated/impersonated rejection. Local trusted catalog/hashes, не server JSON; App/GameData разделены |
| Lease / crash | Exclusive Control lease на весь critical section, повторный preflight, game activity check; durable schema1/job-contract1 intent до native creation, root/SDK startup gates после crash. Atomic JOB_LIST + explicit QUERY witness HANDLE_LIST, protected per-user/SYSTEM Global Job, collision rejection. Installer-only KILL_ON_JOB_CLOSE, без breakaway/fallback; Minecraft no-kill Job не менялся |
| Recovery / safety | Живой owner/active Job/unknown/denied state блокирует retry. Missing installer Job допускает recovery только по подтверждённому install-contract1, не JVM rules. Нет timeout cleanup, deletion/rollback GameData или locator. Verified повтор не запускает Setup; unmanaged legacy App безопасно отвергается, legacy данные сохраняются |
| Installed verification | Manifest PackId/version/channel/MainExe/x64/runtime requirements, current file hashes, trusted separate Update hash, installed full package и root. Exit0 сам по себе недостаточен |
| Restore / Build | Новый solution restore и build успешны, 0 errors/warnings |
| Packaging | Новый Release self-contained non-single-file publish и vpk161 pack успешны в ранее отсутствовавшем ignored artifacts/install-coordinator-packaging-20261011. PackId/version0.6.0/dev не опубликованы и не установлены. CLI подтвердил прямой VelopackApp.Run в Program.Main; Setup/full/portable/feed созданы, предупреждения только unsigned |
| Full xUnit | Новый итоговый прогон Core399/399 + WPF19/19 = **418/418**, без skip. 46 новых cases; прежние372 сохранены. Crash-process cases выполняются непараллельно |
| New test boundaries | C:/D: paths в injected business tests (не реальная D: установка); actual isolated FS/Unicode/legacy/root contention/sentinels/reparse/unknown intent/space checks; access failure injected, физический ACL disk fault не заявляется. Отдельный crash/recovery прогон10/10: новые4 + прежние6. Hanging backend/caller cancellation бизнес-модель проверена; настоящий hung Setup и его 15-minute deadline не моделировались |
| Native proof | Отдельный ignored prototype: 30/30 installer-tree crash repetitions с handleless child. Свойства относятся к installer-only kill contract, не игровому lifecycle; не входят в xUnit count |
| Настоящий Setup161 | Windows11 25H2 build26200, отдельный diagnostic PackId MinecraftLauncher.InstallCoordinator.20261011, dev0.0.1, custom C: Unicode/spaces в artifacts. Самостоятельный native prototype атомарно запустил настоящий Setup; crash test owner в install hook, Setup survived, Job ActiveProcesses=3, hook release → exit0, Job gone. Измерен layout/Update.exe161 и одинаковый hash release/installed package при разном имени. Штатный uninstall exit0, diagnostic App удалён; source/feed/logs сохранены ignored |

Промежуточные неуспехи не скрываются: sandbox не позволял создать ссылки в пяти старых
symlink tests (1314), поэтому итоговый full выполнен вне sandbox. В новом crash test
исправлено ожидание фактического file-handle rundown после owner exit, без изменения
recovery contract. Один промежуточный full выявил падение неизменённого
StorageRootSafetyTests.AtomicLaunch_PreservesArgumentsEnvironmentDirectoryStreamsAndExitCode
на мгновенном Job ActiveProcesses (1 вместо0); отдельный повтор успешен. Существующий
тест не ослаблен; после изоляции новых process tests полный итоговый прогон успешен.

Самостоятельный Setup proof не является E2E production Coordinator/реального Launcher
v0.6.0 →0.6.1. Доказаны только перечисленные phases/платформа. GUI/extraction/catalog
generation, Win10/UAC/cross-user/restrictive jobs/реальный D:/low-space/ACL/trace,
uninstall/reinstall со stable worlds и upgrade остаются отдельными gates.
Update SDK fallback крупных downloads в LocalAppData после потери доступа к App/packages
**не устранён**; preflight не даёт гарантии all-writes-on-selected-volume. Legacy App
без owned receipt не принимается автоматически; in-place adoption/upgrade требует
следующего дизайна. Unique setup staging/logs сохраняются, автоматической recursive
cleanup нет. Подробный API — [architecture.md](architecture.md), следующий GUI/release
план — [releasing.md](releasing.md). Для этой реализации разрешено только Draft review,
не объявление GUI installer release-ready.

Self-review выявил и закрыл startup TOCTOU: одной проверки intent до VelopackApp.Run
недостаточно; обычный SDK callback теперь удерживает shared Control lease. Новые тесты
доказывают исключение installer во время callback, блокировку SDK при pending/exclusive
state и допуск install hook в настоящем child Job при exclusive owner lease.

### Исторический Velopack SDK/vpk upgrade 1.2.161, 11.10.2026

Актуальная основа после fetch — origin/feat/v0.6.1 `6f5e11e`; PR #10/#11 merged.
Начальное дерево: только два известных документа исследования, сохранены без потери.
Рабочая ветка — chore/v061-velopack-12161, публикация по согласованию в Draft PR
с base feat/v0.6.1; статус/HEAD смотреть по GitHub ветке выше и Git.

| Новый прогон | Фактический результат |
| --- | --- |
| Pins / tool restore | App, Infrastructure, test-only UpdateE2EHarness и local vpk — 1.2.161. dotnet tool restore успешен; dotnet vpk -h сообщает CLI 1.2.161. CLI не поддерживает --version, версия проверена через help |
| Restore / build | Solution restore успешен; build --no-restore: 0 errors, 0 warnings. Отдельный UpdateE2EHarness restore/build также 0 errors/warnings |
| Полный xUnit | Core 353 + App 19 = **372/372**, 0 failed/skipped. Реальные Minecraft/Fabric не запускались в этом этапе |
| Crash/recovery повтор | **23/23**, 0 failed/skipped, отдельный новый прогон StorageRootCrashTests/StorageRootRecoveryTests |
| Format | Solution format --verify-no-changes --no-restore: exit0 |
| Release tooling | ReleaseFeedScriptTests.ps1 passed; неизменённый build-release.ps1 с test-only Version 0.6.1-velopack-test.1 / dev успешно создал Setup/full/portable/index/assets/SHA256SUMS. Output paths отсутствовали до запуска, старые artifacts не удалялись; production version в Directory.Build.props остаётся 0.6.0 |
| Mixed SDK/native installed update | Новый ignored artifacts/velopack-upgrade-12161-20261011, отдельный PackId MinecraftLauncher.VelopackUpgrade.20261011, dev, без shortcuts. Host0.0.1 SDK/vpk158 → host0.0.2 SDK/vpk161: check/download/apply/restart exit0, identity/channel/custom install root с пробелами сохранены. Full/delta/portable/Setup/index созданы; download evidence указывает готовый full package |
| Native Update.exe | До update FileVersion/ProductVersion1.2.158, SHA256 4b2bfeed811543e2d37be8743f2b1ea46dd947bc09c8ada9eb0383c0d7886cdb. После apply/restart и startup hook — 1.2.161, SHA256 acefb4a2cb46cc77ed3c2364db17f8bd2a25e2197cfeae56cd85e88a7ac21ac5; совпадает с vendor update_x64.exe161. Это измерение одной цепочки, не гарантия одной лишь смены NuGet pin |
| Fresh Setup161 / uninstall | Настоящий Setup161 --silent --installto custom C: path: exit0, hook successful, обычный launch отсутствует; installed SDK/native161 подтверждены. Оба диагностических uninstall успешны, current и отдельная HKCU registration удалены |
| Data/cache boundaries | World sentinel и sandbox locator SHA256 не менялись после update, uninstall и reinstall. PackagesDir=App/packages, AppTempDir=App/packages/VelopackTemp; native temp/log OS paths не равны гарантии всех writes на выбранном томе. Исходники/пакеты/логи/JSON оставлены ignored |
| Scope | Семь tracked файлов: четыре pins и Ledger/architecture/releasing. Нет изменений .cs/UI/StorageRootResolver/режимов/RAM/protocol/версии/остальных зависимостей; main/release/production/VHDX не меняются |
| Markdown / diff | 17 Markdown-документов, 82 внутренние файловые ссылки и fences проверены, git diff --check чистый. В итоговый diff не включены untracked binaries/логи/PoC или секреты |

Прототип сначала получил CA1416 из-за net10.0 target в Windows-only helper; исправлен
только ignored host target на net10.0-windows. Его повторные сборки чистые; packaging
предупреждает только об отсутствии подписи, не о placement VelopackApp.Run.
Сохранены предыдущие architecture PoC и его исторические результаты ниже.

Совместимость настоящего installed Launcher v0.6.0 → v0.6.1 **не подтверждена этим PoC**:
изолированная Windows account/VM не предоставлена, текущий пользовательский PackId/root
не использован. Перед релизом обязателен отдельный stable-to-candidate E2E с обоими
режимами, settings/world hashes, guardian lifecycle и проверкой native updater после restart.
Win10, D:/low-space, ACL/fallback faults, UAC/cross-user и полный native write trace
в этом этапе не выполнялись. Upgrade готов к независимому review; следующий Coordinator
PR требует отдельного согласования и сохраняет preflight/installation lease gates.

### Исторический GUI Installer Architecture & Feasibility, 11.10.2026

Fetch/GitHub подтверждают merged PR #10/#11; рабочая и origin/feat/v0.6.1 на
`6f5e11eced389416bb243804539b19f549cea5c5`, origin/main bd98ecd. Исходное дерево чистое.
Прочитаны AGENTS, Ledger, architecture/releasing, build-release и текущий bootstrap/resolver.
NuGet/tool cache и бинарная справка подтверждают SDK/vpk 1.2.158; зависимости не обновлялись.
Исследованы pinned Setup/MSI/locator/update/uninstall и официальный compare с 1.2.161.

Предложение: собственный per-user русский GUI-bootstrapper над silent Setup --installto,
не stock MSI. ConfigureNewRoot до native Setup под исходным не-elevated пользователем;
actual App target передаётся в существующий resolver. Legacy/configured GameData остаётся
на месте; same-volume App/GameData относится к clean install, не автоматической миграции.
Нужны coordination на весь install lifetime, проверка не-пустого App перед silent overwrite,
fail-closed cache/update paths (SDK содержит unwritable-App fallback на LocalAppData),
и исключение large self-extraction в C: до выбора тома. Предпочтителен небольшой native GUI
с .NET storage helper в staging выбранного тома; этот packaging ещё не прототипирован.
Подробная схема/риски/PR и Windows acceptance matrix: [releasing.md](releasing.md).

Новый isolated PoC (ignored artifacts/installer-feasibility-20261011) использует отдельный
diagnostic PackId, self-contained host и actual compiled resolver, не текущий Launcher.
Initial configure/reopen/idempotence/legacy refusal успешны. Native Setup silent успешно
установил host в custom C: path с пробелами вне LocalAppData и не выполнил обычный запуск.
Installed local-feed 0.0.1 → 0.0.2 check/download/apply/restart подтвердил сохранение
RootAppDir, App/packages и App/packages/VelopackTemp. Последующий штатный uninstall удалил
только diagnostic App/registration; SHA-256 sandbox locator и world sentinel неизменны.
Исходники/пакеты/JSON/logs оставлены для повторения. Pack warnings: unsigned и async
state-machine placement VelopackApp.Run; production entry point/подпись не менялись.
Single-file helper дополнительно запущен с per-process temp/extract-base внутри lab,
exit0; значение environment подтверждено, полный native write/extraction trace не снят.

Не выполнялись GUI/MSI, low-space/D:/UAC/cross-user или stable v0.6.0 installed update.
Launcher restore/build/test/format не повторялись: прежние 372/372 — исторические.
VHDX/D: read-only inventory; никаких mount/unmount/format/игровых записей. Файл VHDX
на C: не доказывает независимость D: от нехватки backing disk space.
На том этапе изменены только Ledger/releasing и ignored PoC, без commit/push/merge/release;
архитектура и SDK/vpk upgrade требовали отдельного согласования. Последующее разрешение
и фактические результаты SDK/vpk upgrade 1.2.161 описаны выше.

### GitHub-интеграция после подтверждённого smoke, 11.10.2026

PR #10 переведён из Draft и объединён merge-коммитом `cc30be0` только в feat/v0.6.1.
Ветка PR #11 fast-forward до сохранённого эталона `a9f8642`, затем merge актуального
origin/feat/v0.6.1: published c0a37e1 остаётся предком, история не переписана.
Перенесены три semantic resolutions (CmlLib/game-launch/Ledger), дополнительные
CmlLib root/mode и StorageRootCrash tests, architecture. Новых изменений поведения нет.
Test-ветка сохранена неизменной; VHDX не монтировался/не удалялся/не изменялся агентом.

| Новый прогон после адаптации | Результат |
| --- | --- |
| Production/smoke reference | git diff --exit-code a9f8642 -- src tests Directory.Build.props .config .github scripts: exit0. Production-код и тесты совпадают с пользовательской smoke-сборкой; отличаются только документационные записи |
| Restore | dotnet restore MinecraftLauncher.sln: успешно |
| Build | dotnet build MinecraftLauncher.sln --no-restore: 0 warnings/errors |
| Полный xUnit | **Core 353 + App 19 = 372/372**, 0 failed/skipped; новый прогон, не предыдущий результат |
| Crash/recovery повтор | **23/23**: шесть реальных crash-process cases и 17 recovery cases, 0 failed/skipped |
| Format | dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore: exit0 |
| JVM opt-in checks | Предыдущие 16/16 на идентичном guardian/production коде; в этом публикационном этапе не повторялись и не обозначаются новым прогоном |
| GitHub review | PR #10 на контрольном HEAD: 14 файлов, нет новых comments/reviews/unresolved threads. Локальный diff PR #11 к обновлённой базе: 40 файлов — Storage Root + согласованные adaptations, без повторных изменений UI/Core PR #10; опубликованный diff дополнительно сверяется перед merge |
| GitHub CI | Для исходного PR #10 нет statuses/workflow runs. Текущий workflow запускается только для main; локальные результаты не выдаются за CI |
| Git/Markdown | 17 Markdown-документов и 81 внутренняя файловая ссылка проверены; fences/targets/секреты/diff-check чистые. Нет binaries/build outputs/удалений. Итоговый опубликованный diff сверяется с проверенным tree перед merge; main/release/tag/Release не меняются |

Пользовательское свидетельство владельца, Windows11 (не агентский прогон):

- Legacy GameData C:: Server и Singleplayer работают, мир сохраняется после полного
  JVM restart; закрытие Launcher не завершает Minecraft, второй Launcher блокирует
  занятый профиль, после выхода Minecraft блокировки освобождаются.
- Custom `D:\MinecraftLauncher\GameData` на виртуальном NTFS: root назначен через
  ConfigureNewRoot; settings/logs, Minecraft/Fabric/Java/libraries/mods находятся на D:.
  Оба режима работают, мир `StorageRoot-Test` сохраняется после JVM restart;
  Repair не изменил SHA-256 level.dat, конфликтующий Launcher блокируется,
  после завершения игры профиль доступен. `C:\LauncherVHDTest\TestDisk.vhdx` сохраняется.

Результаты smoke применимы потому, что production-код идентичен эталону. Installed
update/packaging/GUI installer, real SMB mapped drive и cross-session/UAC ещё не доказаны.
Гарантия lifetime — основная JVM; согласованное исключение handleless descendant после
main exit сохранено. Автоматическая v0.6.0 migration/новый UI/RAM/SDK/protocol не добавлены.
Состояние merge PR #11/итоговый HEAD проверяется по PR/GitHub, а не предполагается заранее.

### Историческая совместная локальная интеграция PR #10 → PR #11, 11.10.2026

Новый прогон выполнен на объединённом коде, а не переиспользован из отдельных PR.
Исходное дерево чистое; original PR heads и main/integration/release refs не изменены.
Три конфликта: Ledger, game-launch и CmlLibGameLaunchService. Сохранены roadmap PR #10,
история SR-001/SR-002/SR-003, CreateLaunchOption(mode) и internal CreateInstancePath
с shared assets/libraries/versions/runtime. Остальной guardian/Job/recovery код
байт-в-байт совпадает с PR #11; mode/UI/coordinator/factory — с PR #10.

| Новая проверка | Результат |
| --- | --- |
| Restore | Успешен; sandbox не прочитал user NuGet.Config, разрешённый повтор успешен. Версии/зависимости не менялись |
| Build | `dotnet build MinecraftLauncher.sln --no-restore`: 0 warnings/errors |
| Полный xUnit | `dotnet test MinecraftLauncher.sln --no-build --no-restore`: **Core 353 + App 19 = 372/372**, 0 failed/skipped |
| CmlLib/root/mode | Реальные CmlLib options/ProcessStartInfo на synthetic metadata: Server/Singleplayer × legacy/QuickPlay × custom/legacy root, **8 cases** (четыре дополнительных). Instance cwd/gameDir внутри выбранного root, shared mapping одинаков; Server сохраняет endpoint, Singleplayer не передаёт endpoint/QuickPlay |
| Межпроцессные crash cases | Усилены все шесть cases guardian intent/created/state × custom/legacy root: при живом child оба coordinator Play, Repair и Delete отклоняются **до вызова сервисов**. После exit оба dispatch pipeline восстанавливаются, world sentinel сохранён. Child здесь cmd, а coordinator game/pack services тестовые, не Minecraft/Fabric |
| Реальные JVM/production guardian | **16/16 новых checks**: Java17/21 × java.exe/javaw.exe × три фазы (12), наследующий ProcessBuilder descendant (2), Java25 handleless descendant created/state (2). Основная JVM остаётся защищённой после закрытия Launcher lease и crash guardian; восстановление root/profile после exit37. В handleless cases recovery выполнен при живом descendant после main exit — согласованное ограничение |
| Pack/legacy/filesystem | Существующие tests actual PackSync hash/staging/Repair, unmanaged worlds/user files, legacy settings, root unavailable/no-fallback, reparse leaves, ACL, locks/recovery проходят в полном новом прогоне |
| WPF/commands | 19/19 App tests, оба режима/IsBusy/cancel/exit; caption/DPI и compact/keyboard layout сохранены. Native окно на текущем DPI150%, прочие масштабы synthetic |
| Format | `dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore`: exit0. Только изменённые файлы приведены к CRLF/UTF-8 без BOM |
| Целевой повтор | **36/36**, 0 failed/skipped: CmlLib modes/root, crash/recovery, configured-root PackSync/Repair и legacy settings/worlds |
| Markdown/Git | 17 Markdown-документов, 81 внутренняя файловая ссылка: targets/fences/conflict markers проверены; git diff --check чистый. Удалений и бинарных файлов в совокупном diff нет |
| Minecraft/Fabric и installed E2E | Не выполнялись. Владелец согласовал ручной smoke после отчёта; упаковка/установщик/update не входят в этот этап |

Граница доказательств: новые CmlLib cases проверяют mode→arguments и выбранный root;
production JVM harness проверяет общий mode-independent guardian контракт. Это не
end-to-end запуск Minecraft из UI в обоих режимах. Не заявляются actual SMB mapped
drive, другая interactive Windows session/UAC, новый installed update или защита
произвольных потомков без witness после main exit. Таймаутной очистки/fallback нет.

Исторический сценарий ручного smoke, предложенный до пользовательского подтверждения:

1. Отдельный Windows-пользователь/VM без существующих MinecraftLauncher/Control данных,
   App вне тестового GameData. До первого запуска назначить пустой локальный test root
   по schema1 locator (`schemaVersion`, `gameDataRoot`) либо через ConfigureNewRoot API;
   не изменять указатель текущего пользовательского профиля и не переносить реальные worlds.
2. Запустить именно собранный MinecraftLauncher.exe из этой test-ветки под тестовым
   пользователем. Подключить отдельный тестовый Launcher server/profile с валидными v1
   JSON и test Fabric pack, не production. Проверить nickname/RAM и startup log data root.
3. Singleplayer: sync/preparation → главное меню без connect; F3/log подтверждают Fabric
   и ожидаемые mods. Java cwd и `--gameDir` — `<test root>/instances/<profileId>`.
   Создать мир `integration-smoke`, сохранить/выйти; `saves` существует именно здесь.
4. Server: тот же profile/root/shared files, автоматический вход на отдельный test server.
   Проверить версии/моды и отсутствие новой установки; закрытие Launcher не закрывает игру.
5. После выхода игры повторно открыть Launcher/Singleplayer и мир, сохранить второй marker.
   Repair сохраняет level.dat, screenshots/resourcepacks/shaderpacks и другие unmanaged файлы.
   Проверить второй Launcher: Play/Repair/Delete этого профиля блокируются при живой игре.
6. Записать commit сборки, root/cwd, profile/MC/Fabric/pack версии, exit codes, результат
   server connect, reload мира и логи без секретов. Packaging/installed update — отдельный gate.

Вердикт локальной автоматической интеграции: **READY TO INTEGRATE** — подтверждённых
неустранённых safety/data-loss/launch regressions не выявлено. Это не разрешение GitHub
merge и не release readiness. На тот момент ручной smoke был согласован после отчёта,
результат ещё отсутствовал (последующее подтверждение записано выше). Исходные PR/ветки
на локальном этапе оставались неизменными, новая test-ветка
сохраняется только локально. Историю двух feature-веток сохраняют два no-ff merge-коммита;
их SHA проверяются по Git, не дублируются в собственном документе.

### Исторические проверки (не результаты текущей интеграции)

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

### История проверок независимой ветки Storage Root (не совместный прогон)

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

1. PR #10/#11/#12 merged; совместный game код ранее подтверждён владельцем smoke.
   Installation Coordinator реализован отдельной feature-веткой от c5b1f66 для Draft review;
   независимое code review и дальнейший merge требуют отдельного решения.
2. Test-ветку a9f8642 и пользовательский VHDX сохранить. Main/release не изменять;
   tags/Release/force push запрещены. После публикации Coordinator Draft PR остановиться.
3. Следующий **отдельно согласуемый** этап — русский native GUI/managed helper IPC,
   trusted catalog/extraction выбранного тома; ещё отдельно updater fail-closed fallback,
   legacy App adoption и installed v0.6.0 update. Проверить clean D:/почти полный C:,
   packaging/uninstall/reinstall с сохранностью worlds/settings. Не считать текущий PoC release E2E.
4. UI/UX/RAM/ping — v0.6.2; Storage Manager — v0.6.3; несколько источников — v0.7.0.
   Старые status/roadmap/handoff не удаляются, Pack Manager независим.

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
