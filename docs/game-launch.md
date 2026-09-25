# Запуск игры в MVP-2

## Поток запуска

1. Выбранный `GameProfile` преобразуется в `GameLaunchRequest`. Проверяются profile id, версии, Fabric, nickname, RAM и server address.
2. Создаётся `%LOCALAPPDATA%\MinecraftLauncher\instances\<profile-id>` с отдельными `mods`, `config`, `saves`, `screenshots` и игровыми логами.
3. CmlLib.Core проверяет Minecraft version из профиля и получает недостающие официальные Mojang metadata, client, libraries и assets.
4. По version metadata CmlLib подготавливает совместимую Mojang Java runtime внутри managed-каталога Launcher. Java из случайного `PATH` не выбирается.
5. Через Fabric metadata проверяется и устанавливается ровно `loader.version` из профиля. `latest` автоматически не подставляется; для запуска используется version id, возвращённый installer.
6. CmlLib строит процесс с offline session, выбранными Xmx, консервативным Xms, instance path и server address/port.
7. Launcher запускает процесс через `ProcessWrapper`, фиксирует PID, пишет output с префиксом `[Minecraft]` и ждёт exit code.

## Хранение и повторный запуск

Assets, libraries, versions и Java runtime общие для профилей, а изменяемая игровая директория изолирована по profile id. CmlLib проверяет реальные файлы и докачивает недостающие; собственный marker `installed=true` не используется. Поэтому прерванную подготовку можно продолжить, а повторный запуск переиспользует уже скачанные данные.

Перед установкой проверяется не менее 4 GiB свободного места. Launcher ничего не пишет в `%APPDATA%\.minecraft` и не вмешивается в данные официального Minecraft Launcher.

## Progress, cancellation и lifecycle

UI получает реальные file/task и byte-progress события CmlLib. Известный total отображается процентом, количеством файлов или байтами; без total используется indeterminate progress.

Кнопка «Отмена» отменяет поддерживающие `CancellationToken` этапы до старта Minecraft. После старта процесса она исчезает и не используется для принудительного завершения игры. Атомарный guard не позволяет второму Play создать параллельный процесс.

Нулевой exit code возвращает состояние «Minecraft завершён». Ненулевой код показывает короткую ошибку пользователю; полный игровой output и технические исключения остаются в launcher log.

## Ограничения

- Поддерживается только `loader.type = fabric`.
- Сессия локальная offline; Microsoft/Xbox authentication и обход online-mode не реализованы.
- `manifestUrl` не загружается, mods/config не синхронизируются, Repair отсутствует до MVP-3.
- Серверный профиль не может задавать Java path, JVM arguments, environment variables, локальные пути или executable URL.

## CmlLib.Core

Используется NuGet-пакет CmlLib.Core 4.0.6 под лицензией MIT. При распространении приложения следует включать MIT copyright/license notice зависимости; он сохранён в [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).
