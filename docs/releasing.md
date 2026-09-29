# Выпуск Minecraft Launcher

## Зафиксированная конфигурация

- .NET SDK / target: .NET 10, `net10.0-windows`;
- runtime: `win-x64`;
- publish: self-contained, не single-file;
- Velopack NuGet: `1.2.158`;
- repository-local `vpk`: `1.2.158` в `.config/dotnet-tools.json`;
- PackId: `lDiestrol.MinecraftLauncher`;
- PackTitle: `Minecraft Launcher`;
- MainExe: `MinecraftLauncher.exe`;
- authoritative version: `Directory.Build.props`;
- channels: `dev`, `stable`;
- code signing: отсутствует, сборки unsigned.

Application install root Velopack и постоянный data root различны: `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher` и `%LOCALAPPDATA%\MinecraftLauncher`. В installer входят только publish-файлы приложения. Minecraft, Fabric, Java, assets, libraries, instances, mods/config, settings и cache не входят.

## Локальная упаковка

```powershell
./scripts/build-release.ps1 -Version 0.5.0-rc.1 -Channel dev
```

Скрипт проверяет SemVer и allowlist канала до тяжёлой сборки, восстанавливает локальные tools/packages, публикует self-contained приложение, сверяет `ProductVersion` основного EXE, передаёт общую product icon в pinned `vpk pack`, проверяет обязательные artifacts и создаёт lowercase SHA-256 строки в `SHA256SUMS.txt`.

Результат находится в `artifacts/releases/<channel>`. Временный publish — `artifacts/publish/<version>`. Скрипт очищает только эти dedicated output paths. Для локальной последовательной упаковки A → B с delta используется `-KeepPreviousReleases`; vpk сам решает, возможно ли создать delta.

Test-only `tools/Launcher.UpdateE2EHarness` позволяет проверить installed A → B через локальный feed. Он не входит в solution packaging и production installer, не принимает remote URL из UI/server config и предназначен только для ручного E2E.

## GitHub Actions

`.github/workflows/package.yml` запускается только вручную (`workflow_dispatch`) с inputs `version` и `channel`. Windows job выполняет restore/build/test/format, затем тот же release script, проверяет hash manifest и загружает каталог Velopack как Actions artifact. Permissions ограничены `contents: read`; workflow не создаёт tag или GitHub Release.

## Будущий официальный release

После отдельного решения владельца:

```text
validated commit → reviewed tag → package → GitHub Release
→ загрузка Velopack assets и releases.<channel>.json
→ installed Launcher видит update → download → explicit restart/apply
```

Production update source уже закреплён за публичным `lDiestrol/minecraft-launcher`; токен в клиент не встраивается. Public GitHub API имеет rate limits, поэтому check может временно завершаться friendly error без влияния на запуск игры.

Для rollback публикуется новая, более высокая исправленная версия в том же channel. Не заменяйте содержимое уже опубликованной версии: feed, package hashes и пользовательские проверки должны оставаться воспроизводимыми. Downgrade не включён.

RC следует получать только из официального GitHub repository/release source. SHA-256 подтверждает целостность полученного файла, но не заменяет Authenticode identity. Коммерческая подпись может быть добавлена позже отдельным решением без изменения PackId.
