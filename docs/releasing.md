# Выпуск Minecraft Launcher

Текущая stable-версия, verified main и последние результаты проверок находятся в
[PROJECT_LEDGER.md](PROJECT_LEDGER.md). Эта инструкция не является разрешением
создать tag, опубликовать Release или менять версию приложения.

## Зафиксированная конфигурация

- .NET SDK / target: .NET 10, `net10.0-windows`;
- runtime: `win-x64`;
- publish: self-contained, не single-file;
- Velopack NuGet: `1.2.161`;
- repository-local `vpk`: `1.2.161` в `.config/dotnet-tools.json`;
- PackId: `lDiestrol.MinecraftLauncher`;
- PackTitle: `Minecraft Launcher`;
- MainExe: `MinecraftLauncher.exe`;
- authoritative version: `Directory.Build.props`;
- channels: `dev`, `stable`;
- code signing: отсутствует, сборки unsigned.

Application install root Velopack и постоянный data root различны: `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher` и `%LOCALAPPDATA%\MinecraftLauncher`. В installer входят только publish-файлы приложения. Minecraft, Fabric, Java, assets, libraries, instances, mods/config, settings и cache не входят.

## Историческое контролируемое обновление SDK/vpk 1.2.161, 11.10.2026

Отдельно согласовано после архитектурного исследования: App, Infrastructure,
test-only UpdateE2EHarness и repository-local vpk закреплены на 1.2.161.
Версия приложения остаётся 0.6.0; PackId/MainExe/GithubSource/stable/dev и layout
не менялись. На этом историческом этапе Installer/Coordinator не создавались.
Последующая реализация Coordinator описана ниже; история исследования158 сохранена.
Причина обновления и upstream change — [release 1.2.161](https://github.com/velopack/velopack/releases/tag/1.2.161);
это не доказательство совместимости настоящей пользовательской установки.

Новые tool/solution restore, build 0 errors/warnings, xUnit372/372, crash/recovery23/23,
format verify и ReleaseFeedScriptTests успешны. CLI version подтверждена dotnet vpk -h
(--version не поддерживается). Неизменённый build-release.ps1 реально выполнен с
test-only Version0.6.1-velopack-test.1, Channeldev, KeepPreviousReleases в ранее отсутствовавших
dedicated outputs: Setup/full/portable/feed/assets/SHA256SUMS созданы. Ничего не опубликовано
в Releases; версия Directory.Build.props не повышена. Packaging warnings — unsigned files.

Новый ignored PoC: artifacts/velopack-upgrade-12161-20261011, diagnostic PackId
MinecraftLauncher.VelopackUpgrade.20261011, dev, без shortcuts. Не настоящий Launcher.
Host0.0.1 (SDK/vpk158) установлен silent в custom C: path с пробелами вне LocalAppData;
host0.0.2 (SDK/vpk161) упакован в тот же local feed. Full/delta созданы, check/download/apply/
restart успешны. Download evidence содержит готовый full package; не утверждается
проверка всех вариантов delta fallback. AppId/channel/root сохранены, реальные cache paths:
App/packages и App/packages/VelopackTemp. Полный native write trace не снимался;
служебный native uninstall temp использует OS TEMP, поэтому all-writes-on-D не доказано.

**Корневой updater измерен отдельно от SDK:** до update FileVersion/ProductVersion1.2.158,
SHA256 4b2bfeed811543e2d37be8743f2b1ea46dd947bc09c8ada9eb0383c0d7886cdb;
после apply/restart и startup hook FileVersion/ProductVersion1.2.161,
SHA256 acefb4a2cb46cc77ed3c2364db17f8bd2a25e2197cfeae56cd85e88a7ac21ac5.
Новый hash совпадает с vendor update_x64.exe161. Из этого нельзя выводить, что одно
редактирование SDK pin или любое прерванное обновление сразу заменит Update.exe:
проверка относится к завершённой диагностической цепочке с новым startup lifecycle.

Чистая установка новым Setup161 --silent --installto также exit0; install hook успешен,
обычный launch отсутствует. Штатный uninstall диагностического App удалил current/HKCU
registration, сохранил соседний GameData и sandbox Control. Их hashes неизменны после
update, uninstall/reinstall и финального uninstall:

- World sentinel: b5f5311ca80ba278779b312bf9e79eb364dc628a46ef3acdd9b9be7c16fe014b.
- Locator: 423397135926d35870f30bf019d8cd72ce2b4a1ba21368893936037dc095cab0.

Исходники, publish/feed, logs и JSON измерений сохранены ignored для локального повторения;
в upgrade PR включены только pins и документация, без диагностических binaries.
Реальный stable0.6.0 → candidate Launcher E2E не выполнен: отдельный Windows user/VM
не предоставлен, текущие installation/Control/GameData не трогались. Обязательный
release gate: такой upgrade, оба режима/guardian/world/settings и native updater version
на Win10/11, выбранном D: и при faults/низком месте; Authenticode отсутствует.
Fallback locator на LocalAppData при unwritable App остаётся отдельным Coordinator
preflight/fault-injection требованием, SDK upgrade его не устраняет. UAC/cross-user,
GUI/MSI, low-space и настоящий D:/VHDX packaging здесь не проверены.

## Installation Coordinator: контракт следующего bootstrapper

Основа — объединённый [PR #12](https://github.com/lDiestrol/minecraft-launcher/pull/12),
feat/v0.6.1 `c5b1f66c468685d92b90024cbba0f3744609012c`. SDK/vpk161, application version,
PackId/MainExe/stable/dev/GithubSource не меняются. Coordinator не является готовым GUI,
не добавляет entry point установщика и не заменяет обычный update flow.
Managed API и installation lease описаны в [architecture.md](architecture.md).

Следующий native GUI должен запуститься asInvoker под исходным пользователем, получить
его SID, предложить выбранный локальный volume/base и извлечь доверенный self-contained
**не single-file** helper и payload в dedicated staging выбранного тома. Не извлекать
крупный .NET bundle в C: до выбора пути. Helper работает вне заменяемого App, вызывает
Inspect/Preflight/InstallAsync и возвращает result/progress/errors по отдельно согласуемому
IPC. Elevated и impersonated контексты запрещены, SID должен совпасть. Русская UI
оболочка должна показывать Busy/ExistingInstallation/ACL/Space/Recovery без fallback.

Перед стартом: full package manifest, Setup SHA256, package SHA256 и отдельный ожидаемый
Update.exe SHA256 должны быть закреплены доверенным локальным release catalog. Публичный
full `.nupkg` не обязательно содержит updater; его hash нельзя выдумывать из package
entry. Каталог должен учитывать фактический output vpk, в том числе icon/signing.
Сейчас catalog generation/distribution не реализованы и production payload не устанавливался.
Не брать path/hash/launch arguments из bootstrap/profile/manifest игрового сервера.

Схема новой установки — выбранный base/App и base/GameData, временные копии в
base/.launcher-install/<unique token>. Проверяются local fixed/removable volume,
containment/reparse/system/temp intersections, свободное место с reserve и возможность
записи в App/packages/VelopackTemp и небольшом Control. Проверка не резервирует место и
не исключает потерю тома/прав после preflight. При legacy/configured data root сохраняется
старый путь; неизвестная непустая App/другая зарегистрированная установка не заменяется.
Unmanaged v0.6.0 App пока получает безопасный отказ: её in-place upgrade/adoption требует
отдельного дизайна и E2E, не автоматического переноса или второй установки одного PackId.

Coordinator serializes Setup с Launcher/guardian/game через storage lease и durable
intent. Прерванную операцию повторяют с теми же canonical paths и доверенным package.
Нет автоматического удаления App/GameData, миров, backup или rollback locator.
Повреждённый/unknown intent, живой owner/Job и access-denied остаются заблокированы.
Execution timeout/cancel останавливают только новый installer tree и не подтверждают успех.
Успех — проверенный installed layout/root, не один exit0. Pending gate блокирует обычный
SDK startup; отдельный install hook допускается только внутри соответствующего Job.

**Открытый release risk:** Velopack может выбрать LocalAppData для больших update
downloads после потери доступа к App/packages. Coordinator preflight/TEMP/TMP не
устраняет этот SDK fallback. Перед release требуется отдельный fail-closed updater guard
и fault injection/trace. Не обещать «все writes/updates только D:» на основании этого PR.
Unique Setup staging/logs пока сохраняются; безопасная bounded cleanup должна быть
согласована отдельно и не касаться GameData. Native GUI/extraction, unattended policy,
AuthentiCode и uninstall UX также не входят в этот этап.

### Реальные изолированные проверки 11.10.2026

Windows11 25H2 build26200, диагностический PackId
`MinecraftLauncher.InstallCoordinator.20261011`, dev0.0.1, без shortcuts, не Launcher.
Новый self-contained host упакован настоящим vpk161. Native Setup161 установлен в
ignored artifacts на C: с кириллицей/пробелами через самостоятельный atomic installer-job
prototype: test owner принудительно завершён в install hook, Setup остался жив,
Job ActiveProcesses=3; после release hook Setup exit0 и имя Job исчезло. App/current/
packages/Update.exe161 созданы. Release и installed full package имеют разные имена,
но **одинаковый SHA256**. Update161 измерен отдельно и совпал с vendor-no-icon updater.
Штатный silent uninstall exit0 удалил только diagnostic App; источники/feed/logs сохранены.
Это не E2E всего production Coordinator и не installed Launcher0.6.0 →0.6.1.

Отдельный native prototype: 30/30 повторов main installer + handleless child после
crash owner. Child без witness оставался внутри Job; закрытие последнего witness
прекращало этот installer tree, имя не исчезало при наблюдаемом живом child.
Эти 30 повторов не входят в xUnit count и не доказывают поведение на всех Windows.
Production installer Job не изменяет Minecraft Job/no-kill lifetime.

Следующие gates: Windows10/11 VM, реальный обычный пользователь/UAC/другой SID,
restrictive/nested jobs, новый isolated D: и disposable VHDX copy, quotas/full C:/D:,
ACL/detached volume/antivirus, hangs и cancel vendor Setup, trace actual writes,
uninstall/reinstall с world/settings hashes, in-place stable update/full/delta и оба
game modes. Текущий VHDX/`D:\MinecraftLauncher\GameData` и production не использовать
для destructive tests. Графический installer не release-ready до этих проверок.

## Историческое исследование следующего GUI installer, 11.10.2026

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

## Исторический GUI Installer feasibility и предложение архитектуры, 11.10.2026

Исследован merged baseline `6f5e11e` в `feat/v0.6.1`; PR #10/#11 merged.
Это предложение для согласования, не реализация GUI и не разрешение выпуска.
SDK/vpk остаются 1.2.158. Производственные App/GameData/Control не изменены.

**Рекомендуемый вариант — собственный per-user GUI-bootstrapper над native Setup.**
Bootstrapper отвечает за русский интерфейс, выбор базовой папки, preflight,
coordination, initial configuration и проверку результата; Velopack остаётся владельцем
App layout, регистрации, shortcuts, packages и application updates. PackId,
GithubSource и stable/dev не меняются. Bootstrapper не запускается при каждом update.
Публичным Setup.exe должен стать внешний GUI; raw vendor Setup остаётся внутренним
payload, иначе пользователь может обойти выбор папки. Full/delta/feed filenames и
package identity сохраняются; SHA256SUMS должен относиться к внешнему GUI artifact.

MSI технически поддерживает произвольный путь через VELOPACK_INSTALLDIR и сохраняет
Velopack layout, но pinned navigation не открывает BrowseDlg. Добавление picker,
root initialization и rollback потребует собственного MSI authoring/custom actions;
per-machine/UAC противоречит нынешнему per-user Control. Это не простой готовый GUI.
MSI repair/reinstall после Velopack update требует отдельной проверки из-за разных
механизмов владения установленными файлами. Для v0.6.1 MSI не рекомендуется.

Для требования почти полного C: предпочтителен небольшой native Win32 GUI, manifest
asInvoker, static CRT, без отдельного framework bootstrap. Управляемый storage initializer
на .NET 10 вызывает существующий resolver, а не копирует его правила. Его self-contained
payload и native Setup извлекаются только в уникальный private staging выбранного тома;
это дополнительный Windows C++ build step, который нужно согласовать и проверить.
WPF single-file bootstrapper без native stage не является доказанным решением:
[.NET extraction](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview)
при self-extract происходит до managed Main и по умолчанию использует TEMP/.net.
GUI/native orchestration и полный контроль extraction ещё не прототипированы.

Предлагаемый первоначальный lifecycle:

1. Проверить token/current-user identity, существующую установку и locator, не вызывая
   OpenSession для обнаружения чистой установки: этот API может создать legacy root.
   Не выполнять elevation; запуск с elevated/чужим token отклонять до записи Control.
2. Показать `<base>/App` и `<base>/GameData`, проверить canonical local paths,
   NTFS/права/место/reparse/пересечения. Не принимать чужой непустой App:
   silent Setup отвечает yes на overwrite и способен удалить его содержимое.
3. Сериализовать installer/обычные Launcher/guardian операции на всём критическом
   участке, повторно проверить активные игры через существующий root/activity контракт.
   Нужен узкий installation lease/gate с повторным использованием Control locking:
   ConfigureNewRoot сейчас освобождает exclusive lease после вызова и сам по себе
   не является транзакцией установки. Не обходить crash-safe intent/witness.
4. Initializer получает только локально выбранные пути, actual target App directory
   и текущий LocalApplicationData; вызывает ConfigureNewRoot **до native Setup**.
   Это исключает даже ручной запуск опубликованного shortcut до готовности указателя.
   Не полагаться на fast install hook: vendor hook failure не отменяет установку.
5. Запустить Setup --silent --installto `<base>/App`, с log/temp/staging на выбранном
   томе и без shell/string-built command execution. Проверить exit code **и** фактические
   manifest/PackId/version/root/update paths; exit0 не заменяет проверки результата.
   Silent Setup выполняет install hook, но не обычный запуск приложения.
6. Проверить configured root через resolver; только после успеха дать GUI-действие
   «Запустить». Ошибка оставляет понятный retry state, без fallback и удаления миров.
   Pointer/App install не являются общей atomic transaction: после неудачного Setup
   безопасно сохранить новый указатель для retry; не откатывать его без доказанной
   принадлежности и отсутствия новых игровых данных/владельцев.

При legacy v0.6.0 или уже сохранённом root GameData остаётся на прежнем месте.
GUI явно сообщает об этом; выбор другой папки не означает перенос. App существующей
установки обновляется на прежнем месте, вторую установку того же PackId не создавать.
App/GameData на одном выбранном томе — контракт **чистой** установки. Перенос App или
GameData существующего пользователя остаётся отдельным согласуемым направлением.

Обновление использует current/package metadata, сохраняет channel и GithubSource;
работает только с App/current, App/packages и App/packages/VelopackTemp. В SDK/native
[locator 1.2.158](https://github.com/velopack/velopack/blob/1.2.158/src/lib-csharp/Locators/WindowsVelopackLocator.cs)
есть LocalAppData fallback при unwritable App; то же поведение осталось в 1.2.161.
Обязателен fail-closed preflight до SDK initialization и download/apply, проверка
фактических locator paths и отказ от download вне App. Потеря прав/тома между проверкой
и native I/O — отдельный fault-injection gate; отсутствие скрытого C: fallback в этом
сценарии пока не доказано. Update/uninstall могут принудительно остановить App/guardian,
поэтому согласование активной игры должно произойти **до** передачи управления vendor.

Uninstall удаляет только проверенный App и его registration/shortcuts; parent base,
GameData и Control сохраняются. Не добавлять автоматическую data cleanup. Повторная
установка использует сохранённый root. Собственные диалоги всегда русские, native Setup
silent; системные UAC/SmartScreen/dialogs имеют язык OS, не контролируемый приложением.

**Новый изолированный PoC:** ignored artifacts/installer-feasibility-20261011,
отдельный PackId MinecraftLauncher.InstallerFeasibility.20261011, без shortcuts.
Использован закреплённый SDK/vpk 1.2.158 и настоящая уже собранная Infrastructure.
Сборки тестового host 0.0.1/0.0.2 успешны; vpk предупредил об unsigned files и
VelopackApp.Run в async state machine (PoC, не production entry point).

| Фактическая проверка | Результат |
| --- | --- |
| Initial ConfigureNewRoot → OpenSession | Новый соседний GameData сохранён/повторно открыт; повторная та же конфигурация успешна, попытка переноса непустого legacy отклонена |
| Self-contained single-file initializer host | Exit0 с per-process TEMP/TMP/extract-base внутри lab; helper подтвердил значение extract-base. Полный native write trace и фактическая runtime extraction не подтверждены |
| Native Setup --silent --installto | Exit0; custom C: path с пробелами вне LocalAppData; install hook успешен, обычный запуск отсутствует |
| Installed local-feed 0.0.1 → 0.0.2 | Check/download/apply/restart успешны; новый host подтвердил version0.0.2, тот же RootAppDir, PackagesDir и AppTempDir внутри App |
| Uninstall diagnostic package | Exit0; App/current и отдельная HKCU registration удалены; GameData sentinel level.dat и sandbox locator SHA-256 не изменились после update и uninstall |

World SHA-256: 5208ae034c39ebcd4b227b3557f8bd58dc9d75f3bda35b4dfa79b69bc72837ce.
Locator SHA-256: 7a8c13ada6b4fcee040e66a27f95f88381b71b90bf5d51b523ff266403f74227.
Сохранены source/publish/feed/JSON/logs для повторения; установлен только test host,
затем он штатно удалён. Настоящий per-user locator и Minecraft не использовались.

Это не GUI test, не MSI E2E, не реальный v0.6.0 → v0.6.1, не D:/low-space/UAC test
и не новый полный regression Launcher. Предыдущие 372/372 здесь не повторялись.
Read-only volume snapshot: C: NTFS, примерно 393 GiB свободно; D: NTFS Fixed,
32 GiB capacity, примерно 30.8 GiB свободно. VHDX C:/LauncherVHDTest/TestDisk.vhdx
не менялся. Файл VHDX лежит на C:: при динамическом расширении место D: зависит от
backing volume, поэтому он не заменяет независимый физический D: для full-C test.

Официальный [compare 1.2.158...1.2.161](https://github.com/velopack/velopack/compare/1.2.158...1.2.161)
содержит пять изменённых файлов: native runtime_arch.rs и четыре Node/npm файла;
C# API, MSI template и root locator не изменены. Предлагается отдельно согласовать
SDK+vpk 1.2.161; это сильное свидетельство низкого API migration риска, не доказательство
совместимости установленного stable пакета. App version/production release не менялись.
В installed update отдельно снимать версии SDK и корневого Update.exe: upgrade
NuGet/vpk не доказывает автоматическую замену уже работающего native updater.

Предлагаемые небольшие PR после согласования: (1) SDK/vpk upgrade, если разрешён,
с compatibility tests; (2) initializer + installation lease/preflight/retry tests,
без нового game root implementation; (3) русский GUI/native payload extraction и
packaging без изменения production source; (4) opt-in Windows installer/update/uninstall
E2E и release checklist. Не смешивать эти этапы с RAM/UI Launcher/Storage Manager.

Windows acceptance matrix: Win10 x64 22H2 и поддерживаемые LTSC/Enterprise, Win11
x64 актуальные поддерживаемые версии; ordinary/admin split tokens, другой UAC account,
ru/en OS, Unicode/spaces/long paths, DPI100/150/200%, keyboard-only. На disposable VM
проверить чистую установку C:/physical D:/VHDX D:, legacy in-place upgrade и reinstall;
непустой чужой App, links/UNC/mapped network/system paths/отказ ACL; почти полный C:
и D:, quota/backing-file exhaustion, missing/detached/read-only disk, transient I/O.
Каждый install/update failure/crash/cancel проверить до/после locator и App publication;
параллельные installer/Launcher/guardian/игра должны безопасно блокироваться.
Успешный update проверяется по restart version/root/cache/channel/shortcuts/registration,
full и delta packages, offline/corrupt download и retry. Uninstall/reinstall и оба игровых
режима проверяются с Fabric/Java/shared mapping и world/settings/user-file SHA manifest.
Наш VHDX/root сохранять: новые тесты только в отдельном каталоге или disposable копии,
не форматировать и не заполнять текущие тома. SDK trace/Process Monitor должен подтвердить
реальные write paths; только после этих gates installer считается release-ready.

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
