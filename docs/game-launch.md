# Запуск игры в Launcher v0.6.0

Актуальный статус и планы launch modes: [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
Описанный ниже поток реализован; одиночный режим относится к следующей v0.6.1.

Локальный этап Storage Root в `feat/v061-storage-root` создан независимо от ещё
не объединённого [Draft PR #10](https://github.com/lDiestrol/minecraft-launcher/pull/10).
Режимы запуска здесь не меняются. `<GameData>` определяется общим StorageRootResolver
для App/guardian; legacy/default — `%LOCALAPPDATA%\MinecraftLauncher`.

## Поток запуска

Локальный guardian использует crash-safe intent v3/witness v1 и атомарный JOB_LIST,
QUERY witness наследуется основной JVM через explicit HANDLE_LIST. Защита и восстановление
проверены с реальными Java17/21, но Minecraft smoke/installed update ещё не выполнялись;
требуется независимое повторное review, см. [PROJECT_LEDGER.md](PROJECT_LEDGER.md).

1. Выбранный `GameProfile` преобразуется в `GameLaunchRequest`. Проверяются profile id, версии, Fabric, nickname, RAM и server address.
2. `LauncherOperationCoordinator` получает профильную межпроцессную блокировку и выполняет Pack Manifest v1 sync: полный SHA-256, загрузка/проверка staging, публикация и managed cleanup. При ошибке pack Minecraft не запускается; Repair заканчивается после этого этапа.
3. Используется `<GameData>\instances\<profile-id>` с отдельными `mods`, `config`, `saves`, `screenshots` и игровыми логами; проверяется свободное место на выбранном томе. Настроенный root обязан существовать; при его недоступности новый каталог не создаётся.
4. Под shared-install lock CmlLib.Core проверяет Minecraft version и получает недостающие официальные Mojang metadata, client, libraries и assets.
5. По version metadata CmlLib подготавливает совместимую Mojang Java runtime внутри managed-каталога Launcher. Перед запуском executable повторно проверяется на существование, reparse points и принадлежность `<GameData>\runtime`; Java из случайного `PATH` не выбирается.
6. Через Fabric metadata проверяется и устанавливается ровно `loader.version` из профиля. `latest` автоматически не подставляется; для запуска используется version id, возвращённый installer.
7. CmlLib строит параметры процесса с offline session, выбранными Xmx, консервативным Xms, instance path и server address/port. v0.6.0 всегда передаёт серверный endpoint.
8. `GameProcessGuardianClient` передаёт запуск guardian-процессу, который запускает Java, фиксирует PID и пишет output с префиксом `[Minecraft]`. После защищённой передачи shared-install lock освобождается; guardian удерживает профильную защиту до выхода игры. Launcher ждёт exit code через `GuardedGameSession`.

## Хранение и повторный запуск

Assets, libraries, versions и Java runtime общие для профилей, а изменяемая игровая директория изолирована по profile id. CmlLib проверяет реальные файлы и докачивает недостающие; собственный marker `installed=true` не используется. Поэтому прерванную подготовку можно продолжить, а повторный запуск переиспользует уже скачанные данные.

Перед установкой проверяется не менее 4 GiB свободного места. Launcher ничего не пишет в `%APPDATA%\.minecraft` и не вмешивается в данные официального Minecraft Launcher.

## Progress, cancellation и lifecycle

UI получает реальные file/task и byte-progress события CmlLib. Известный total отображается процентом, количеством файлов или байтами; без total используется indeterminate progress.

Кнопка «Отмена» отменяет поддерживающие `CancellationToken` этапы до старта Minecraft. Для Fabric-запросов CmlLib 4.0.6, у которых нет token-overload, Launcher отменяет pending-запрос своего выделенного game `HttpClient` и проверяет token до и после вызова. После старта процесса кнопка исчезает и не используется для принудительного завершения игры.

Profile lock не допускает второй Play/Repair/Delete того же профиля, включая другое окно Launcher. Shared-install lock отдельно сериализует подготовку общих файлов. Guardian сохраняет профильную защиту при закрытии Launcher. Отмена после решения guardian о старте завершает защищённую передачу, а не убивает игру или преждевременно освобождает её защиту.

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

## CmlLib.Core

Используется NuGet-пакет CmlLib.Core 4.0.6 под лицензией MIT. При распространении приложения следует включать MIT copyright/license notice зависимости; он сохранён в [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
