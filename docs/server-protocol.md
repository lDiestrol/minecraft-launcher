# Server Protocol v1

Этот документ задаёт статический HTTP-контракт между Launcher MVP-3 и nginx/static hosting/server implementation.

## Входной URL и нормализация

Launcher принимает:

- базовый URL `https://example.org` или `https://example.org/`;
- прямой JSON URL, например `https://example.org/custom/bootstrap.json`.

Базовый URL нормализуется в `https://example.org/launcher/bootstrap.json`. Любой абсолютный URL, путь которого заканчивается на `.json` без учёта регистра, используется напрямую. Fragment удаляется; query прямой JSON-ссылки сохраняется.

Разрешён HTTPS. Обычный HTTP разрешён только для loopback (`localhost`, `127.0.0.1`, `::1`) в локальной разработке. URL с embedded username/password отклоняются.

## bootstrap.json

Стандартный путь:

```text
/launcher/bootstrap.json
```

Контракт:

```json
{
  "schemaVersion": 1,
  "serverName": "Example Minecraft Server",
  "profilesUrl": "/launcher/profiles.json",
  "defaultProfileId": "main"
}
```

Обязательны `schemaVersion`, непустой `serverName` и непустой `profilesUrl`. `defaultProfileId` может отсутствовать или быть `null`.

`profilesUrl` бывает относительным или абсолютным. Launcher разрешает его относительно фактического bootstrap URL через `System.Uri`, а не конкатенацию строк:

```json
{ "profilesUrl": "/launcher/profiles.json" }
```

```json
{ "profilesUrl": "https://cdn.example.org/profiles.json" }
```

Удалённый абсолютный URL также должен использовать HTTPS; loopback HTTP разрешён для разработки.

## profiles.json

Контракт:

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

`profiles` должен содержать хотя бы один полностью корректный элемент. Для каждого элемента обязательны непустые `id`, `name`, `minecraftVersion`, `loader.type`, `loader.version`, `packVersion`, `manifestUrl`, `serverAddress` и `serverPort` в диапазоне 1–65535. Profile IDs уникальны без учёта регистра.

`id` содержит от 1 до 64 ASCII-букв, цифр, `-` или `_`; Windows device names (`CON`, `NUL`, `COM1` и аналогичные) запрещены. `minecraftVersion` и `loader.version` содержат не более 64 безопасных символов версии и не допускают path separators. В MVP-3 исполняется только `loader.type = "fabric"`; версия loader используется точно как передана, без автоматической замены на `latest`.

Server Protocol v1 разбирается строго: неизвестные поля отклоняются. В частности, профиль не может передать `executable`, Java path, JVM arguments, environment variables или локальную игровую директорию.

`manifestUrl` указывает на отдельный Pack Manifest v1. Относительный URL разрешается относительно фактического `profiles.json`; абсолютный URL поддерживается с теми же HTTPS/loopback правилами. Перед Play и Repair manifest загружается и сверяется с `profileId`, `packVersion`, `minecraftVersion` и точной версией loader. Schema описана в [pack-manifest.md](pack-manifest.md); версия Server Protocol при этом остаётся `1`.

## Версионирование

Оба документа обязаны содержать `schemaVersion: 1`. Отсутствующая, нулевая или отрицательная версия считается повреждённым контрактом. При версии выше 1 Launcher прекращает подключение и сообщает, что конфигурации нужна более новая версия Launcher. Неизвестная версия не обрабатывается частично или молча.

## HTTP и ошибки

Endpoints должны возвращать успешный HTTP status и непустой JSON с media type `application/json` (дополнительные JSON media types допустимы, поскольку содержимое разбирается как JSON).

Launcher обрабатывает timeout, DNS/connection/TLS failures, HTTP 4xx/5xx, пустой ответ, malformed JSON, неизвестную schema и malformed profiles без падения. Технические сведения записываются в локальный лог, пользователю показывается короткое сообщение. Настройки нового сервера сохраняются только после успешной проверки обоих документов.

Автоматические HTTP redirects для config-клиента отключены. Launcher вручную обрабатывает 301, 302, 303, 307 и 308, разрешает относительный `Location` от текущего URL и проверяет каждый target по HTTPS/loopback-правилам до отправки следующего запроса. Допускается не более пяти redirects; отсутствующий, некорректный или запрещённый `Location` отклоняется без запроса к target.

Manifest и pack files используют отдельный download-клиент с тем же HTTPS/loopback правилом и отключёнными automatic redirects. Relative file URLs разрешаются от фактического final manifest URI.

Размер ответа `bootstrap.json` ограничен 256 KiB, а `profiles.json` — 1 MiB. Ответ с большим `Content-Length` отклоняется до чтения body; для chunked-ответа лимит контролируется во время потокового чтения.
