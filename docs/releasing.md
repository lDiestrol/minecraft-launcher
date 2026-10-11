# Выпуск Minecraft Launcher

Текущая stable-версия, verified main и последние результаты проверок находятся в
[PROJECT_LEDGER.md](PROJECT_LEDGER.md). Эта инструкция не является разрешением
создать tag, опубликовать Release или менять версию приложения.

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

## Исследование следующего GUI installer, 11.10.2026

Installer в Storage Root этапе **не создаётся**; версия app/SDK/vpk остаётся прежней.
GameData можно первоначально назначить через локальный `StorageRootResolver.ConfigureNewRoot()`;
Control хранится вне App в per-user LocalApplicationData (подробнее [architecture.md](architecture.md)).
Будущий installer должен передать actual target App directory в resolver, сохранить
указатель **до первого обычного запуска** под учётной записью пользователя, не
создавать большие данные в App и не мигрировать существующий v0.6.0 root молча.

Проверены именно закреплённые binaries/source `1.2.158`, а не только текущий сайт:

- Справка vendor `setup_x64.exe --help` подтверждает `-t/--installto <DIR>`;
  это выбор через аргумент, не GUI folder picker. Обычный Setup — one-click.
- Восстановленный local `vpk 1.2.158 pack --help` содержит `--msi`, `--msiVersion`,
  `--instLocation`, `--msiTopBanner` и `--msiDialogBackground`.
- [MSI template 1.2.158](https://github.com/velopack/velopack/blob/1.2.158/src/vpk/Velopack.Packaging.Windows/Msi/Templates/MsiTemplate.hbs)
  поддерживает `VELOPACK_INSTALLDIR` и per-user/per-machine scope. BrowseDlg включён,
  но штатная navigation не открывает его: Welcome/InstallScope → VerifyReady.
  Готовый GUI выбора произвольной папки на этом основании **не подтверждён**.
- [Официальная инструкция](https://docs.velopack.io/packaging/installer) описывает
  сохранение updates через Update.exe после Setup/MSI. Pinned template сохраняет
  Velopack layout/stub/current/packages. Реальное MSI/custom-directory installed update
  E2E здесь не выполнялось; нельзя считать документацию доказательством E2E.

[NuGet 1.2.158](https://www.nuget.org/packages/Velopack/1.2.158) deprecated с причиной
`CriticalBugs`; registration/catalog metadata не содержит более подробного объяснения.
[Release 1.2.161](https://github.com/velopack/velopack/releases/tag/1.2.161) исправляет
Windows architecture detection без IsWow64Process2;
[issue #1071](https://github.com/velopack/velopack/issues/1071) описывает отказ native x64
Setup на старых Windows (Server 2016/Win10 1607). Это подтверждённая связанная регрессия,
но metadata не утверждает, что только она вызвала deprecation. Не считать deprecation
доказанным security advisory или проблемой сохранности Minecraft данных.

Предложение: согласовать отдельно переход **SDK и vpk вместе** на 1.2.161 до installer
PR, проверив поддерживаемые OS и installed v0.6.0 → новая версия. Без разрешения
зависимости не обновляются. Затем выбрать GUI bootstrapper поверх `Setup --installto`
либо доработанную MSI-схему с root locator; нужны тесты прав/per-user identity,
temp/package paths при почти полном C:, сохранности legacy данных и update/restart.

## Локальная упаковка

```powershell
./scripts/build-release.ps1 -Version 0.6.0 -Channel stable
```

Скрипт проверяет SemVer и allowlist канала до тяжёлой сборки, восстанавливает локальные tools/packages, публикует self-contained приложение, сверяет `ProductVersion` основного EXE, передаёт общую product icon в pinned `vpk pack`, проверяет обязательные artifacts и создаёт lowercase SHA-256 строки в `SHA256SUMS.txt`.

Перед публикацией скрипт оставляет в `releases.<channel>.json` только Full/Delta текущей версии. Это важно для `GithubSource`: каждая запись feed связывается с тем GitHub Release, в котором находится индекс, поэтому исторические пакеты не должны повторно указываться из нового Release. Проверенный предыдущий full package и legacy `RELEASES-<channel>` могут оставаться рядом локально для построения delta, но не включаются в publish-only `SHA256SUMS.txt`.

Результат находится в `artifacts/releases/<channel>`. Временный publish — `artifacts/publish/<version>`. Скрипт очищает только эти dedicated output paths. Для локальной последовательной упаковки A → B с delta используется `-KeepPreviousReleases`; vpk сам решает, возможно ли создать delta.

Test-only `tools/Launcher.UpdateE2EHarness` позволяет проверить installed A → B через локальный feed. Он не входит в solution packaging и production installer, не принимает remote URL из UI/server config и предназначен только для ручного E2E.

## GitHub Actions

`.github/workflows/package.yml` запускается только вручную (`workflow_dispatch`) с inputs `version` и `channel`. Windows job выполняет restore/build/test/format, затем тот же release script, проверяет hash manifest и загружает каталог Velopack как Actions artifact. Permissions ограничены `contents: read`; workflow не создаёт tag или GitHub Release.

## Официальные releases

Стабильная v0.6.0 опубликована в [GitHub Releases](https://github.com/lDiestrol/minecraft-launcher/releases/tag/v0.6.0). Её installed update path с v0.5.0 подтверждён пользователем.

Каждый следующий release выполняется только после отдельного решения владельца:

```text
validated commit → reviewed tag → package → GitHub Release
→ загрузка Velopack assets и releases.<channel>.json
→ installed Launcher видит update → download → explicit restart/apply
```

Production update source уже закреплён за публичным `lDiestrol/minecraft-launcher`; токен в клиент не встраивается. Public GitHub API имеет rate limits, поэтому check может временно завершаться friendly error без влияния на запуск игры.

Для rollback публикуется новая, более высокая исправленная версия в том же channel. Не заменяйте содержимое уже опубликованной версии: feed, package hashes и пользовательские проверки должны оставаться воспроизводимыми. Downgrade не включён.

Стабильные и предварительные сборки следует получать только из официального GitHub repository/release source. SHA-256 подтверждает целостность полученного файла, но не заменяет Authenticode identity. Коммерческая подпись может быть добавлена позже отдельным решением без изменения PackId.
