# Единый журнал Minecraft Launcher

> Проверено 10 октября 2026 года, часовой пояс Asia/Yekaterinburg (UTC+05:00).
> Стабильная ветка: `main`; verified commit после `git fetch origin`:
> `bd98ecd721298981508d54466d11089c1647ab4f`.
> Стабильная версия — **v0.6.0**. Следующая программная разработка — **v0.6.1**.
> Последняя локальная проверка после исправления WPF/DPI-теста: **252/252 проходят**.
> Исправление находится локально в `fix/v061-windowchrome-dpi`, commit/push не выполнялись.

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
| Стабильная ветка | `main`, verified `origin/main` — `bd98ecd721298981508d54466d11089c1647ab4f`; рабочий HEAD документации отслеживается отдельно |
| Интеграционная ветка | `feat/v0.6.1`, создана от `bd98ecd721298981508d54466d11089c1647ab4f`, подтверждена fetch |
| Verified main | [bd98ecd721298981508d54466d11089c1647ab4f](https://github.com/lDiestrol/minecraft-launcher/commit/bd98ecd721298981508d54466d11089c1647ab4f) |
| Стабильный release | [v0.6.0](https://github.com/lDiestrol/minecraft-launcher/releases/tag/v0.6.0), GitHub API: `draft=false`, `prerelease=false`, опубликован 09.10.2026 |
| Release tag/target | [1ae3ae74b1b70d92031fe8706f80f85e506efd56](https://github.com/lDiestrol/minecraft-launcher/commit/1ae3ae74b1b70d92031fe8706f80f85e506efd56) |
| Версия исходников | `0.6.0` в [Directory.Build.props](../Directory.Build.props) |
| Runtime/packaging | Self-contained `win-x64`, Velopack `1.2.158`, PackId `lDiestrol.MinecraftLauncher`, stable channel |
| Подпись | Authenticode отсутствует; пакеты unsigned |

Tag релиза и verified main различаются: после релизного commit в main добавлена
документация через PR #7. `git diff v0.6.0..origin/main` показывает только документацию.
Названия старых feature/release-веток не задают актуальную основу разработки.

На 10.10.2026 GitHub API и fetch показывают ветки `main`, `codex/project-status-roadmap`,
`feat/first-release`, `feat/mvp-shell`, `feat/mvp2-game-launch`, `feat/ui-refresh`,
`fix/github-release-feed-index`, `release/v0.6.0`; повторный fetch подтвердил новую
`feat/v0.6.1` от `bd98ecd`. Ветки v0.6.2 в проверенном списке нет; это не доказывает
отсутствие работы вне публичного Git. Ветка документационной задачи —
`docs/unified-agent-state`, от той же verified основы, PR направляется в `feat/v0.6.1`.

Согласованная 10.10.2026 Git-стратегия: main сохраняет стабильную v0.6.0;
промежуточные feature/docs/fix PR объединяются в `feat/v0.6.1` только в рамках
разрешённой задачи. Полностью завершённая v0.6.1 позже переносится в main одним
Squash & Merge после явного согласования владельца. Этот документационный PR
публикуется как Draft для независимого review, без merge, tags или Release.

## 3. Версии и отдельные направления

| Версия / этап | Состояние | Объём и подтверждение |
| --- | --- | --- |
| MVP-1 | Завершён | Shell/onboarding; исторический commit `664b6ed` |
| MVP-2 | Завершён | Minecraft/Fabric launch; старый branch snapshot `e77be5f` содержит 125 тест-кейсов |
| MVP-3 / pack sync | Уже реализован в стабильной основе | Pack Manifest v1, SHA-256 sync, staging, Repair; начинать этап заново нельзя |
| 0.4.0-rc.1 / 0.5.0-rc.1 | Исторические release notes | Сохранены в Git; публикация этих RC сейчас отдельно не проверялась |
| v0.5.0 | Завершён, исторический stable | Tag `v0.5.0`, commit `c0e3990`; основа проверенного ранее installed update |
| v0.6.0 | Текущий stable, опубликован | Dark Gaming UI, profile file management, locks/guardian, app updates и release tooling |
| v0.6.1 | Согласован, следующий программный этап | Серверный/одиночный запуск одного профиля, точный RAM, небольшие UX-исправления; сейчас не реализован |
| v0.6.2 | Согласован, запланирован после v0.6.1 | Размер данных, безопасное удаление установки профиля, отдельные shared-data операции |
| Серверный Pack Manager | Отдельный согласованный трек проектирования | Publisher, версионирование клиентских pack, планируемый LXC/NGINX; реализация и deployment не подтверждены |
| Единая документация агентов | Подготовлена для Draft review | Владелец разрешил один документационный commit/push и Draft PR в `feat/v0.6.1`; merge/release не разрешены |

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
к v0.6.1. Начальная фраза ViewModel о ручной проверке не описывает уже реализованный
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

Только два режима запуска одного профиля, точный ввод RAM и небольшие UX-исправления.
Server Protocol v1, Pack Manifest v1 и формат settings остаются совместимыми.
Отдельные локальные сборки, storage manager и серверная админка сюда не входят.

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

### 5.2. Точный RAM в мегабайтах

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

### 5.3. Ограниченная UX-полировка и порядок работ

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

## 6. План v0.6.2 — согласованный отдельный этап

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
| Application/data roots | `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher` / `%LOCALAPPDATA%\MinecraftLauncher` |
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
| WPF test failure, 10.10.2026 — исправлен локально | До исправления `CaptionButtonsExecuteTheirWindowActions`, assertion line 63: ожидалось `ActualHeight=41`, реально `41.333333333333336`. Диагностика на актуальной `feat/v0.6.1` подтвердила DPI 150% и унаследованный `UseLayoutRounding=True`: 41 DIP округляется до 62 физических пикселей, затем 62 / 1.5 DIP. Исправлены DPI-aware ожидания теста после согласования; действия кнопок сохранены, UI не менялся, полный прогон 252/252 |
| Исторические 252 passed | Записаны для релиза 09.10; диагностические 251 passed/1 failed на ноутбуке и последующие 252 passed после исправления теста — отдельные результаты, не переписывающие историю релиза |
| Atomic publish терминология | File.Replace/Move атомарны пофайлово; нет journal/backup/rollback всей pack-транзакции. I/O failure или crash в apply могут оставить частично обновлённый pack и старый state. Нельзя обещать полный crash rollback |
| Длина архитектурных классов | MainViewModel/PackSyncService совмещают много обязанностей; возможный рефакторинг — технический долг, не согласованная новая функция v0.6.1 |
| Startup update сообщение | Начальная фраза ViewModel о ручной проверке расходится с автоматическим startup check; записано для UX review |
| Portable/code signing | Portable apply E2E не повторён; Authenticode отсутствует; решение о подписи и UX остаётся открытым |
| Старые документы | Разные baseline SHA и путь ПК в status/handoff; сохранены как исторические снимки, текущие правила/планы перенесены сюда |
| Исправления документации | Уточнены code-behind, профильно scoped coordinator/guardian, pack sync в launch flow, пофайловая atomicity и фактическое имя Setup asset |
| Release notes v0.6.0 | Фраза «после публикации» — исторический текст релиза; актуальная публикация подтверждена API. Notes сохранены без переписывания истории |

Диагностика WindowChrome (10.10.2026): исходное рабочее дерево было чистым;
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
Результаты текущих проверок и ограничения multi-DPI проверки — в разделе 12.

## 10. Нерешённые вопросы

Перед v0.6.1: проверить CmlLib quick-connect options и полученные arguments обоих
режимов; место второй кнопки в compact 900×650/DPI; UX clamp/validation при известных
RAM min/max; достоверное получение/отображение Velopack channel в installed,
portable и development mode; разделение сообщений блокировки Play/Repair/Delete.

Перед v0.6.2: состав установки профиля; пересечение managed config и личных настроек;
достоверный shared-file usage; необходимость preview; частичные ошибки cleanup/rollback.

Для Pack Manager: фактические public URL, NGINX roots/cache/invalidation,
ownership/permissions и текущий publishing source; backup/retention/rollback;
LXC OS/quota/storage/network/firewall/service credentials; VM lifecycle и health timeout;
client/server/common mod mapping; authoritative pack version, publisher/reviewer roles,
storage atomic primitives и требуемый audit export. Детали остаются в отдельном плане.

Прочие вопросы: ревью локального DPI-aware исправления WPF-теста; portable update UX/E2E;
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
| 10.10.2026 | Затем согласована интеграция через `feat/v0.6.1`; fetch подтвердил ветку от `bd98ecd`. Создан документационный commit, ветка `docs/unified-agent-state` отправлена в origin и опубликован [Draft PR #8](https://github.com/lDiestrol/minecraft-launcher/pull/8) — `docs: unify agent instructions and project state`, base `feat/v0.6.1`. Публикация подтверждена Git/GitHub; merge не выполнялся |
| 10.10.2026 | По замечанию P2 независимого review исправлены устаревшие записи о публикации: сохранена хронология локального черновика и последующего Draft PR #8. Проверены Markdown, внутренние ссылки и `git diff --check`; изменён только этот Ledger, код и WPF-тест не исправлялись |
| 10.10.2026 | В `fix/v061-windowchrome-dpi` воспроизведён WPF-сбой: отдельный тест 0/1, полный прогон Core 241/241 и App 10/11 (всего 251/252). Причина подтверждена измерением DPI, обратимым переключением rounding в памяти и изолированным layout при пяти DPI; действия всех caption buttons работают. Исходники не менялись, исправление предложено на согласование |
| 10.10.2026 | Владелец согласовал исправление теста; в той же fix-ветке добавлены строгие проверки заданных размеров и DPI-aware фактических размеров всех трёх кнопок. Restore/build/format успешны, отдельный тест 1/1, полный прогон 252/252 без пропусков; compiled helper проверен на пяти DPI и двух смешанных X/Y масштабах. Изменены только тест и Ledger; commit/push/PR/merge не выполнялись |

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

Текущие проверки исправления, 10.10.2026, локальная `fix/v061-windowchrome-dpi`:

| Проверка | Результат |
| --- | --- |
| Git/fetch/scope | HEAD совпадает с актуальной интеграционной основой; незакоммичены только тест и Ledger, диагностические записи сохранены |
| `dotnet restore MinecraftLauncher.sln` | Успешно с разрешённым доступом; первоначальная попытка в песочнице не смогла прочитать пользовательский NuGet.Config |
| `dotnet build MinecraftLauncher.sln --no-restore` | Успешно; 0 warnings, 0 errors |
| Отдельный `CaptionButtonsExecuteTheirWindowActions`, `--no-build --no-restore` | 1/1 passed; реальное окно на этом компьютере при DPI 150% |
| `dotnet test MinecraftLauncher.sln --no-build --no-restore` | Core 241/241, App 11/11; всего 252 passed, 0 failed, 0 skipped |
| `dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore` | Успешно, exit 0; форматирование не меняло файлы |
| Multi-DPI и midpoint | Скомпилированный helper проверен на изолированном WPF layout с SetRootDpi: 100/125/150/175/200%, дополнительно X/Y 125/175% и 175/125%. Обе оси и midpoint-to-even соответствуют WPF |
| Контроль регрессий helper | Отклонены неправильные Width/Height, отключённый rounding и отсутствие layout (нулевые Actual-размеры) |
| Markdown/ссылки/`git diff --check`/scope | Проверены структура Ledger, внутренние файловые ссылки и diff; изменения ограничены `WindowChromeBehaviorTests.cs` и этим Ledger |

Полный оконный сценарий теста выполнялся при фактическом DPI 150%; другие масштабы
проверены на изолированном layout, а не на физических мониторах. Проверки helper
выполнялись диагностическими командами в памяти, новых тест-кейсов/файлов не добавлено.
Версия, зависимости, MainWindow.xaml и UseLayoutRounding приложения не менялись;
commit, push, создание PR и merge требуют отдельного согласования.

## 13. Следующие действия и миграция документов

1. Независимое review Draft PR документации в `feat/v0.6.1`; решение о merge принимается
   отдельно. Старые status/roadmap/handoff сохраняются до отдельного согласования удаления.
2. Получить опубликованную docs-ветку для review на нужных компьютерах; после
   согласованного merge синхронизировать Ledger/AGENTS через `feat/v0.6.1`, на каждой
   машине проверить remote/branch/HEAD. Стабильный main в этом шаге не меняется.
3. Выполнить ревью локального исправления WPF/DPI-теста: зелёная baseline 252/252
   восстановлена; commit/push/PR согласовать отдельно. Диагностика — в разделе 9.
4. После согласования документации начать программную задачу v0.6.1: launch mode,
   затем RAM и UX; версию приложения менять только в отдельной release preparation.
5. После v0.6.1 проектировать v0.6.2; Pack Manager вести отдельными design/implementation PR.

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
