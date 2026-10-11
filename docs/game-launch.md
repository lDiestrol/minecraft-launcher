# Запуск игры: стабильная основа v0.6.0 и реализация v0.6.1 для review

Актуальный статус и планы launch modes: [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
Общий поток реализован в стабильной основе. Два режима и Storage Root объединены
локально в `test/v061-pr10-pr11-integration` от `origin/feat/v0.6.1`; это ещё не выпуск
v0.6.1. Версия приложения и зависимости остаются прежними.

## Режимы одного профиля

- «Играть»: `GameLaunchMode.Server`, прежнее автоматическое подключение к адресу
  и порту выбранного серверного профиля.
- «Одиночная игра»: `GameLaunchMode.Singleplayer`, запуск в главное меню без
  автоматического подключения и без выбора мира через QuickPlay.

Намерение задаётся явно в Core, а не пустым server address. `GameLaunchRequestFactory`
по умолчанию сохраняет `Server` для существующих вызовов. Оба запроса содержат те же
profile id, Minecraft/Fabric, nickname, RAM и данные исходного серверного профиля;
отличается только `Mode`. Server Protocol v1, Pack Manifest v1 и settings не менялись.

В CmlLib параметры `ServerIp`/`ServerPort` назначаются **только** для `Server`.
Для `Singleplayer` не назначаются endpoint, QuickPlaySingleplayer, QuickPlayRealms
или QuickPlayPath. Числовой default `MLaunchOption.ServerPort` без `ServerIp` не
является аргументом запуска: CmlLib не добавляет ни legacy `--server`/`--port`, ни
`--quickPlayMultiplayer`. Для серверного режима CmlLib выбирает QuickPlay, если он
поддержан version metadata, иначе сохраняет legacy connect arguments.

Одиночная игра использует тот же instance, `mods`, `config` и `saves`: отдельный
локальный профиль не создаётся. Обычный pack sync обязателен перед обоими запусками;
это не автономный offline-режим без launcher server. Совместимость конкретного
серверного модпака с локальными мирами требует отдельного реального smoke-теста.

Исходные [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10) и
[Draft PR #11](https://github.com/lDiestrol/minecraft-launcher/pull/11) остаются открытыми.
`<GameData>` для обоих режимов определяется общим StorageRootResolver
для App/guardian; legacy/default — `%LOCALAPPDATA%\MinecraftLauncher`.

## Поток запуска

Локальный guardian использует crash-safe intent v3/witness v1 и атомарный JOB_LIST,
QUERY witness наследуется основной JVM через explicit HANDLE_LIST. Защита и восстановление
проверены в истории PR #11 с реальными Java17/21. Новый совместный прогон и ограничения
Minecraft smoke/installed update приведены в [PROJECT_LEDGER.md](PROJECT_LEDGER.md).

1. Выбранный `GameProfile` и явный режим преобразуются в `GameLaunchRequest`. Проверяются допустимость режима, profile id, версии, Fabric, nickname, RAM и server address.
2. `LauncherOperationCoordinator` получает профильную межпроцессную блокировку и выполняет Pack Manifest v1 sync: полный SHA-256, загрузка/проверка staging, публикация и managed cleanup. При ошибке pack Minecraft не запускается; Repair заканчивается после этого этапа.
3. Используется `<GameData>\instances\<profile-id>` с отдельными `mods`, `config`, `saves`, `screenshots` и игровыми логами; проверяется свободное место на выбранном томе. Настроенный root обязан существовать; при его недоступности новый каталог не создаётся.
4. Под shared-install lock CmlLib.Core проверяет Minecraft version и получает недостающие официальные Mojang metadata, client, libraries и assets.
5. По version metadata CmlLib подготавливает совместимую Mojang Java runtime внутри managed-каталога Launcher. Перед запуском executable повторно проверяется на существование, reparse points и принадлежность `<GameData>\runtime`; Java из случайного `PATH` не выбирается.
6. Через Fabric metadata проверяется и устанавливается ровно `loader.version` из профиля. `latest` автоматически не подставляется; для запуска используется version id, возвращённый installer.
7. Общий helper CmlLib строит параметры процесса с offline session, выбранными Xmx, консервативным Xms и instance path. Серверный endpoint добавляется только в режиме `Server`; остальная подготовка одинакова.
8. `GameProcessGuardianClient` передаёт запуск guardian-процессу, который запускает Java, фиксирует PID и пишет output с префиксом `[Minecraft]`. После защищённой передачи shared-install lock освобождается; guardian удерживает профильную защиту до выхода игры. Launcher ждёт exit code через `GuardedGameSession`.

## Хранение и повторный запуск

Assets, libraries, versions и Java runtime общие для профилей, а изменяемая игровая директория изолирована по profile id. CmlLib проверяет реальные файлы и докачивает недостающие; собственный marker `installed=true` не используется. Поэтому прерванную подготовку можно продолжить, а повторный запуск переиспользует уже скачанные данные.

Перед установкой проверяется не менее 4 GiB свободного места. Launcher ничего не пишет в `%APPDATA%\.minecraft` и не вмешивается в данные официального Minecraft Launcher.

## Progress, cancellation и lifecycle

UI получает реальные file/task и byte-progress события CmlLib. Известный total отображается процентом, количеством файлов или байтами; без total используется indeterminate progress.

Кнопка «Отмена» отменяет поддерживающие `CancellationToken` этапы до старта Minecraft. Для Fabric-запросов CmlLib 4.0.6, у которых нет token-overload, Launcher отменяет pending-запрос своего выделенного game `HttpClient` и проверяет token до и после вызова. После старта процесса кнопка исчезает и не используется для принудительного завершения игры.

Profile lock не допускает второй Play/Repair/Delete того же профиля, включая другое окно Launcher. Shared-install lock отдельно сериализует подготовку общих файлов. Guardian сохраняет профильную защиту при закрытии Launcher. Отмена после решения guardian о старте завершает защищённую передачу, а не убивает игру или преждевременно освобождает её защиту.

Оба UI-command вызывают один `PlayAsync(mode)` и один coordinator. Во время
подготовки и работы Minecraft `IsBusy` отключает оба запуска, Repair/Delete,
смену сервера/настроек и app update. Статусы подготовки, работы, отмены и завершения
указывают выбранный режим. После sync перед передачей в game service повторно
проверяется cancellation token. Вторая кнопка использует существующий QuietButton,
AutomationProperties.Name и стандартную клавиатурную навигацию; общий UI не переделан.

App/guardian дополнительно удерживают storage session в per-user Control; выбор root
нельзя изменить при открытом Launcher или активном guardian. Guardian сначала читает
общий локальный root locator, затем валидирует request path и RootDirectory; request
не выбирает root. App-host installed/portable/development запускается как прежде;
при development-запуске через `dotnet MinecraftLauncher.dll` guardian получает путь
entry assembly перед приватными аргументами. Pack staging остаётся внутри instance
на том же томе назначения; формат guardian request, pack manifest и server protocol не меняется.

После crash guardian защита не зависит от живого handle Launcher: основной JVM передаётся
query-only witness именованного Job. Recovery под exclusive `.game.lock` допускается лишь
при завершённом guardian и пустом Job либо отсутствующем имени по поддерживаемому
intent v3/witness-v1 контракту; AccessDenied/unknown state fail-closed, таймаут очистки запрещён.
Legacy intent без witness не применяет missing-Job pre-PID recovery. Пока существует Job
с активными процессами, Play/Repair/Delete/root reconfiguration блокируются.
Если основная JVM вышла, а потомок без witness остался внутри Job и имя исчезло,
восстановление разрешено: **произвольные потомки после main exit не входят в lifetime гарантию**.
Отсутствие имени не доказывает пустоту дерева. Guardian crash сообщает ошибку ожидания
status/exit code, но не снимает durable защиту активной основной JVM и не убивает её.

Нулевой exit code возвращает состояние «Minecraft завершён». Ненулевой код показывает короткую ошибку пользователю; полный игровой output и технические исключения остаются в launcher log.

## Ограничения

- Поддерживается только `loader.type = fabric`.
- Сессия локальная offline; Microsoft/Xbox authentication и обход online-mode не реализованы.
- Pack Manifest v1 управляет только `mods/` и `config/`; Repair выполняет полный hash/sync cycle без запуска Minecraft.
- Серверный профиль не может задавать Java path, JVM arguments, environment variables, локальные пути или executable URL.

## Предыдущие проверки локальной реализации, 10.10.2026

- Restore/build/format verify успешны; build: 0 ошибок и 0 предупреждений.
  Полный набор: Core 261/261 + App 19/19 = 280/280, без пропусков.
- Проверены реальные `MLaunchOption` и `MinecraftProcessBuilder.CreateProcess()`
  установленного CmlLib 4.0.6 на синтетических legacy/QuickPlay version metadata.
  Проверка сформированного ProcessStartInfo не запускает Java/Minecraft.
- Тесты обоих режимов проверяют профиль/settings, sync-before-launch и его ошибки,
  отмену, exit code, команды/IsBusy, удержание и освобождение профильной блокировки.
  Отдельный процесс держит реальный lock-файл: оба запуска отклоняются до sync,
  после его выхода профиль снова доступен. Существующие guardian/shared-lock,
  SHA-256, disk-space и update regression tests входят в полный прогон.
- Реальное WPF-окно 900×650 проверено на текущем мониторе (150% DPI): обе кнопки
  видимы в viewport, связаны с нужными командами, доступны по Tab.
  Фактический dashboard layout дополнительно измерен с `SetRootDpi` при
  100/125/150/175/200%; это синтетические масштабы, не пять физических мониторов.
- Реальный Minecraft, вход на сервер, главное меню и создание/открытие мира в этой
  задаче **не запускались**. Также не проверялись packaging/installed update E2E.
  Перед объединением требуется независимое review, затем пользовательский smoke
  обоих режимов с реальным профилем; отсутствие connect arguments не гарантирует,
  что сам модпак не меняет стандартное поведение меню.

На этапе согласованной публикации для независимого Draft-review код/тесты не
менялись, restore/build/test/format повторно не запускались. Указанные результаты
относятся к предыдущему локальному прогону, не к новому CI или реальному Minecraft E2E.
Цель PR — `feat/v0.6.1`; merge и выпуск версии требуют отдельного разрешения.

## CmlLib.Core

Используется NuGet-пакет CmlLib.Core 4.0.6 под лицензией MIT. При распространении приложения следует включать MIT copyright/license notice зависимости; он сохранён в [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
