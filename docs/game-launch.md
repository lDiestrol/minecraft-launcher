# Запуск игры в Launcher v0.6.0

Актуальный статус и планы launch modes: [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
Описанный ниже поток реализован; одиночный режим относится к следующей v0.6.1.

## Поток запуска

1. Выбранный `GameProfile` преобразуется в `GameLaunchRequest`. Проверяются profile id, версии, Fabric, nickname, RAM и server address.
2. `LauncherOperationCoordinator` получает профильную межпроцессную блокировку и выполняет Pack Manifest v1 sync: полный SHA-256, загрузка/проверка staging, публикация и managed cleanup. При ошибке pack Minecraft не запускается; Repair заканчивается после этого этапа.
3. Создаётся `%LOCALAPPDATA%\MinecraftLauncher\instances\<profile-id>` с отдельными `mods`, `config`, `saves`, `screenshots` и игровыми логами; проверяется свободное место.
4. Под shared-install lock CmlLib.Core проверяет Minecraft version и получает недостающие официальные Mojang metadata, client, libraries и assets.
5. По version metadata CmlLib подготавливает совместимую Mojang Java runtime внутри managed-каталога Launcher. Перед запуском executable повторно проверяется на существование и принадлежность `%LOCALAPPDATA%\MinecraftLauncher\runtime`; Java из случайного `PATH` не выбирается.
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

Нулевой exit code возвращает состояние «Minecraft завершён». Ненулевой код показывает короткую ошибку пользователю; полный игровой output и технические исключения остаются в launcher log.

## Ограничения

- Поддерживается только `loader.type = fabric`.
- Сессия локальная offline; Microsoft/Xbox authentication и обход online-mode не реализованы.
- Pack Manifest v1 управляет только `mods/` и `config/`; Repair выполняет полный hash/sync cycle без запуска Minecraft.
- Серверный профиль не может задавать Java path, JVM arguments, environment variables, локальные пути или executable URL.

## CmlLib.Core

Используется NuGet-пакет CmlLib.Core 4.0.6 под лицензией MIT. При распространении приложения следует включать MIT copyright/license notice зависимости; он сохранён в [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
