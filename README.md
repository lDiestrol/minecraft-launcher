# Minecraft Launcher

Открытый Windows-лаунчер для Minecraft-серверов с конфигурацией по URL. Пользователь указывает адрес сервера, лаунчер получает `bootstrap.json` и список игровых сборок, после чего сохраняет ник, выбранный профиль и объём RAM.

Проект находится на этапе **первого публичного Release Candidate (0.5.0-rc.1)**. Перед запуском Minecraft Launcher получает Pack Manifest v1, проверяет SHA-256 и синхронизирует Launcher-managed `mods`/`config`. Затем CmlLib.Core подготавливает Minecraft, managed Java и точную версию Fabric и запускает клиент с локальным offline nickname.

## Требования

- Windows 10 или Windows 11 x64;
- .NET 10 SDK только для сборки из исходников; установленный и portable Launcher self-contained;
- сервер конфигурации с HTTPS (HTTP разрешён только для loopback-адресов при локальной разработке).

## Сборка и запуск

```powershell
dotnet restore MinecraftLauncher.sln
dotnet build MinecraftLauncher.sln
dotnet test MinecraftLauncher.sln
dotnet run --project src/Launcher.App/Launcher.App.csproj
```

Проект совместим с обычным workflow в VS Code и не требует Visual Studio для командной сборки.

## Installation

Обычному пользователю нужен только `lDiestrol.MinecraftLauncher-<version>-Setup.exe` из официального GitHub repository/release source проекта. Setup выполняет per-user установку Windows x64 и не требует отдельно установленного .NET Runtime или прав администратора. Установленное приложение и постоянные данные разделены:

- Velopack application: `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher`;
- settings, логи и Minecraft: `%LOCALAPPDATA%\MinecraftLauncher`.

Удаление или обновление приложения не предназначено для удаления второго каталога. Portable ZIP можно распаковать и запускать без установки. Velopack распознаёт portable mode; отдельный portable A→B apply E2E в рамках этого RC не выполнялся, поэтому основным проверенным update path остаётся версия из Setup.exe.

## SmartScreen

Текущий RC не подписан Authenticode. Windows может показать `Unknown Publisher` или SmartScreen. Для Setup.exe, полученного из официального источника проекта, выберите «Подробнее» → «Выполнить в любом случае». Не отключайте SmartScreen глобально. `SHA256SUMS.txt` позволяет проверить целостность конкретного файла, но не заменяет доверие Authenticode.

## Updates

После показа главного окна установленная версия неблокирующе проверяет GitHub Releases. Если update отсутствует или GitHub временно недоступен, основной UI не меняется и запуск Minecraft не блокируется. Найденная версия показывается ненавязчивым banner с действиями «Обновить» и «Позже»; скачивание и перезапуск никогда не начинаются автоматически.

В настройках, в секции «Обновления Launcher», отображается текущая версия и сохраняется явная ручная проверка:

1. проверить GitHub Releases;
2. скачать найденное обновление с реальным progress;
3. явно выбрать «Перезапустить и обновить».

Обновление не является принудительным и не блокирует Minecraft при ошибке GitHub, timeout или rate limit. Канал (`dev` или `stable`) закрепляется при packaging и определяется Velopack из установленного пакета. Игровой сервер, bootstrap, profile и Pack Manifest не могут менять источник или канал обновления.

## Структура solution

- `src/Launcher.App` — WPF, ViewModels, команды и composition root;
- `src/Launcher.Core` — доменные модели, контракты сервисов, URL/nickname validation и RAM policy;
- `src/Launcher.Infrastructure` — HTTP/JSON, settings, файловые логи, определение физической памяти и интеграция CmlLib.Core;
- `tests/Launcher.Core.Tests` — unit-тесты Core и инфраструктурных границ без реального интернета;
- `docs` — архитектура и Server Protocol v1.

Зависимости направлены от App и Infrastructure к Core. Core не зависит от WPF и Windows UI.

## Подключение по URL

При первом запуске пользователь вводит базовый URL, например `https://example.org`, либо прямую ссылку на JSON. Базовый URL нормализуется в:

```text
https://example.org/launcher/bootstrap.json
```

Лаунчер последовательно загружает и проверяет bootstrap и profiles. URL сохраняется только после полного успеха обоих запросов. При следующем запуске подключение восстанавливается автоматически; сервер можно сменить через «Настройки».

### Пример bootstrap.json

```json
{
  "schemaVersion": 1,
  "serverName": "Example Minecraft Server",
  "profilesUrl": "/launcher/profiles.json",
  "defaultProfileId": "main"
}
```

### Пример profiles.json

```json
{
  "schemaVersion": 1,
  "profiles": [
    {
      "id": "main",
      "name": "Основной сервер",
      "minecraftVersion": "1.20.1",
      "loader": {
        "type": "fabric",
        "version": "0.16.14"
      },
      "packVersion": "1.0.0",
      "manifestUrl": "/launcher/packs/main/manifest.json",
      "serverAddress": "mc.example.org",
      "serverPort": 25565
    }
  ]
}
```

Полный контракт описан в [docs/server-protocol.md](docs/server-protocol.md).

## Пользовательские данные

Лаунчер не пишет настройки рядом с EXE. На Windows используются:

- `%LOCALAPPDATA%\MinecraftLauncher\settings.json` — URL сервера, nickname, профиль и RAM;
- `%LOCALAPPDATA%\MinecraftLauncher\logs\launcher-YYYYMMDD.log` — технический лог.
- `%LOCALAPPDATA%\MinecraftLauncher\instances\<profile-id>` — изолированные игровые данные профиля;
- `%LOCALAPPDATA%\MinecraftLauncher\assets`, `libraries`, `versions` и `runtime` — общие проверяемые CmlLib файлы Minecraft и Mojang Java.

Повреждённые, пустые и частичные settings не приводят к падению: применяются безопасные значения по умолчанию.

## Запуск игры

Первый запуск может занять продолжительное время: Launcher потоково загружает недостающие pack-файлы, проверяет exact size и SHA-256 и только затем публикует их атомарно. После успешной pack sync CmlLib.Core получает официальные metadata и недостающие файлы Minecraft, assets, libraries и managed Java, после чего устанавливает точную версию Fabric Loader из `profiles.json`. UI показывает реальные файловые и byte-progress события; подготовку можно отменить до старта процесса.

При следующих запусках существующие файлы проверяются и переиспользуются. Minecraft получает выбранный объём RAM, offline nickname и адрес/порт сервера. Offline-сессия не обходит Microsoft authentication: она подходит только для серверов, чья конфигурация допускает такой вход.

Кнопка «Проверить файлы» выполняет тот же полный hash/repair cycle без запуска Minecraft. Устаревшие файлы удаляются только из предыдущего managed state; сторонние пользовательские файлы сохраняются. Подробнее: [docs/game-launch.md](docs/game-launch.md) и [docs/pack-manifest.md](docs/pack-manifest.md).

## Текущие ограничения

MVP-4 поддерживает только Windows x64, Fabric, offline nickname и managed-файлы в `mods`/`config`. RC unsigned, поэтому возможен SmartScreen warning. Намеренно отсутствуют Microsoft login, Forge/NeoForge/Quilt, resourcepacks/shaderpacks, telemetry, backend/admin panel и коммерческая code signing. Официальный production GitHub Release на этом этапе не опубликован.

## Публичный репозиторий и secrets policy

Репозиторий публичный. В исходники, конфигурацию и историю Git нельзя добавлять пароли, токены, private keys/certificates, signing keys, внутренние адреса и персональные данные. Секрет, встроенный в клиентский EXE, всегда считается публичным.

Локальный `launcher.local.json` игнорируется Git. Безопасный шаблон находится в `launcher.local.example.json`; приложение не требует этот файл для обычного запуска.

Подробнее об устройстве проекта: [docs/architecture.md](docs/architecture.md).
