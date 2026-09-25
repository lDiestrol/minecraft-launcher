# Minecraft Launcher

Открытый Windows-лаунчер для Minecraft-серверов с конфигурацией по URL. Пользователь указывает адрес сервера, лаунчер получает `bootstrap.json` и список игровых сборок, после чего сохраняет ник, выбранный профиль и объём RAM.

Проект находится на этапе **MVP-1 (0.1.0-dev)**. Сейчас реализована оболочка и сетевой протокол. Скачивание и запуск Minecraft ещё не реализованы; кнопка «Играть» сообщает об этом явно и не имитирует запуск.

## Требования

- Windows 10 или Windows 11 x64;
- .NET 10 SDK для сборки из исходников;
- сервер конфигурации с HTTPS (HTTP разрешён только для loopback-адресов при локальной разработке).

## Сборка и запуск

```powershell
dotnet restore MinecraftLauncher.sln
dotnet build MinecraftLauncher.sln
dotnet test MinecraftLauncher.sln
dotnet run --project src/Launcher.App/Launcher.App.csproj
```

Проект совместим с обычным workflow в VS Code и не требует Visual Studio для командной сборки.

## Структура solution

- `src/Launcher.App` — WPF, ViewModels, команды и composition root;
- `src/Launcher.Core` — доменные модели, контракты сервисов, URL/nickname validation и RAM policy;
- `src/Launcher.Infrastructure` — HTTP/JSON, settings, файловые логи и определение физической памяти;
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

Повреждённые, пустые и частичные settings не приводят к падению: применяются безопасные значения по умолчанию.

## Текущие ограничения

На этапе MVP-1 намеренно отсутствуют загрузка и запуск Minecraft/Java, установка Fabric, pack manifest и синхронизация модов, Repair, аккаунты Microsoft, собственная регистрация, backend, installer и self-update. CmlLib.Core и Velopack не подключены «на будущее».

## Публичный репозиторий и secrets policy

Репозиторий публичный. В исходники, конфигурацию и историю Git нельзя добавлять пароли, токены, private keys/certificates, signing keys, внутренние адреса и персональные данные. Секрет, встроенный в клиентский EXE, всегда считается публичным.

Локальный `launcher.local.json` игнорируется Git. Безопасный шаблон находится в `launcher.local.example.json`; приложение не требует этот файл для обычного запуска.

Подробнее об устройстве проекта: [docs/architecture.md](docs/architecture.md).
