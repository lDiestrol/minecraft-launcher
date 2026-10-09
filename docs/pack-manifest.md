# Pack Manifest v1

Pack Manifest — отдельный от Server Protocol контракт статического HTTP-хостинга. `profiles.json` указывает `manifestUrl`, а manifest перечисляет только Launcher-managed файлы сборки.

## Schema v1

```json
{
  "schemaVersion": 1,
  "profileId": "main",
  "packVersion": "1.0.0",
  "minecraftVersion": "1.20.1",
  "loader": {
    "type": "fabric",
    "version": "0.16.14"
  },
  "files": [
    {
      "path": "mods/fabric-api.jar",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "size": 123456,
      "url": "files/mods/fabric-api.jar"
    },
    {
      "path": "config/server-config.json",
      "sha256": "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
      "size": 321,
      "url": "files/config/server-config.json"
    }
  ]
}
```

Manifest ограничен 4 MiB и 10 000 entries. JSON разбирается строго: неизвестные поля отклоняются. `profileId`, `packVersion`, `minecraftVersion`, `loader.type` и `loader.version` обязаны точно совпадать с выбранным `GameProfile`; `schemaVersion` должен быть равен `1`.

## File entry

- `path` — portable relative path с `/`; разрешены только деревья `mods/` и `config/`;
- `sha256` — ровно 64 hexadecimal symbols, внутри Launcher хранится lowercase;
- `size` — неотрицательный `Int64`, фактический файл должен иметь точный размер;
- `url` — URL содержимого относительно фактического final manifest URI либо абсолютный разрешённый URL.

Пути с `..`, `.`, пустыми segments, `\`, ведущим `/`, drive colon, trailing dot/space, NUL, Windows device names, `.launcher` и абсолютные пути запрещены. Пути уникальны с `StringComparer.OrdinalIgnoreCase`, поэтому `mods/Test.jar` и `mods/test.jar` не могут сосуществовать.

Удалённые endpoints используют HTTPS. HTTP разрешён только для loopback-разработки. `file:`, `ftp:`, `data:`, UNC и remote HTTP запрещены. Redirects 301, 302, 303, 307 и 308 выполняются вручную, максимум пять раз; каждый target проверяется до отправки следующего request. Relative file URL разрешается от final manifest URI после redirects.

## Ownership и удаление

`instances/<profile-id>/.launcher/managed-state.json` — локальная запись Launcher-owned путей:

```json
{
  "schemaVersion": 1,
  "profileId": "main",
  "packVersion": "1.0.0",
  "files": [
    {
      "path": "mods/fabric-api.jar",
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "size": 123456
    }
  ]
}
```

State считается недоверенным: schema, profile, paths, hashes, sizes и case-insensitive duplicates проверяются повторно. Файл удаляется автоматически только если его путь был в предыдущем valid state и отсутствует в новом manifest. Файлы, никогда не записанные в managed state, сохраняются. Если пользователь изменил manifest-managed файл, следующий Sync/Repair восстанавливает опубликованную версию.

## Безопасная синхронизация

Launcher всегда проверяет SHA-256 существующих manifest-файлов, даже если `packVersion`, size и state совпадают. Нужные файлы последовательно загружаются потоково в `instances/<profile-id>/.launcher/staging/<operation-id>`, одновременно считаются bytes и SHA-256. `Content-Length`, если он есть, обязан совпасть с `size`; поток без длины не может превысить ожидаемый размер.

Destination не изменяется до проверки exact size и SHA-256. Verified файл публикуется на том же volume через `File.Move` для нового destination или `File.Replace` для существующего. Сначала подготавливаются все downloads, затем файлы публикуются, после чего удаляются obsolete managed-файлы и атомарно заменяется `managed-state.json`. При HTTP/hash/size/cancellation failure старый destination и старый state остаются нетронутыми; staging очищается best-effort.

Перед записью, заменой или удалением Launcher повторно проверяет containment и все существующие parent directories. Переход через symlink, junction или другой `FileAttributes.ReparsePoint` запрещён. Manifest не управляет runtime, versions, libraries, assets, saves, screenshots, logs, корнем instance или `.launcher`.

Manifest не может задавать executable, Java path, JVM arguments, environment, working directory, команды, scripts, registry или локальные абсолютные пути. Архивы не распаковываются, `Content-Disposition` не влияет на destination.

## Hosting

Достаточно static HTTP hosting:

```text
/launcher/bootstrap.json
/launcher/profiles.json
/launcher/packs/main/manifest.json
/launcher/packs/main/files/mods/...
/launcher/packs/main/files/config/...
```

Pack HTTP client не хранит и не отправляет cookies. Credentials, signed manifests, archives и deployment backend в Launcher v0.6.0 не поддерживаются.
