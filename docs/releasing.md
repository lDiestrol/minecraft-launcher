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

В stable v0.6.0 install root и legacy data root различны: `%LOCALAPPDATA%\lDiestrol.MinecraftLauncher` и `%LOCALAPPDATA%\MinecraftLauncher`. Новая GUI-ветка позволяет выбрать `<Base>\App` и `<Base>\GameData`; существующий locator/legacy GameData сохраняется без переноса. Minecraft, Fabric, Java, assets, libraries, instances, mods/config, settings и cache не входят в release payload.

## Native GUI Bootstrapper: PR #14, 12.10.2026

### Исправление проверки установленного updater, 14.10.2026

Владелец сообщил о Windows10 GUI E2E: Setup161 завершил установку, но verification
ошибочно ожидал `App/current/Squirrel.exe`. В фактическом full package entry
`lib/app/Squirrel.exe` и установленном `App/Update.exe` SHA-256 совпал; остальные
192 файла `lib/app` также прошли проверку по данным владельца. Это внешнее наблюдение,
на Debian оно не повторялось. Backend теперь обрабатывает этот точный entry раньше
общего `lib/app/` → `App/current/`; другие файлы, включая вложенный Squirrel.exe,
сохраняют обычное сопоставление. Размер и SHA-256 сверяются с package entry, затем
отдельно проверяется trusted Update hash. Reparse/containment и остальные gates
`RecordSuccessfulSetup` не изменены. 27 новых синтетических тестов проверяют успех,
отсутствующий/изменённый updater, независимость обеих hash-проверок, обычные файлы,
manifest/package, reparse ancestors и traversal; реальный Setup не вызывается.
Windows CI включён для PR в `feat/v0.6.1`; GUI E2E и release gates остаются отдельными.

Следующее описание и результаты относятся к историческому этапу 12.10.2026.

Основа — PR #13, merged в `feat/v0.6.1` (`9c4f5cfe31aecedd7d8a2eba603e1bcf2951f8a7`).
Реализация находится в отдельной `feat/v061-gui-bootstrapper`. Версия приложения,
SDK/vpk161, PackId/MainExe/channel/GithubSource не меняются. Это Draft для ревью,
**не release-ready установщик** и не доказанный upgrade stable0.6.0 → v0.6.1.

Публичный `lDiestrol.MinecraftLauncher-<channel>-Setup.exe` теперь содержит native
Win32 x64 GUI (RU, UTF-16, asInvoker, PerMonitorV2, keyboard navigation). `/MT`
устраняет отдельную установку VC Redistributable. GUI не требует .NET до выбора тома.
Проверка bundled index читает метаданные без извлечения runtime. После выбора GUI
извлекает файлы с `CreateNew` в `<Base>\.launcher-install\bootstrap-<GUID>` с private
ACL пользователя/SYSTEM, проверкой reparse/containment и SHA-256 каждого файла.
`TEMP/TMP` в environment дочернего helper указывают туда же; global environment не
меняется. Дочерние процессы могут наследовать эти значения, это не запрет всех
vendor/OS writes вне staging.
Managed helper публикуется self-contained win-x64, non-single-file. Installation
Coordinator остаётся единственным владельцем preflight/lease/recovery/Setup/verification.

`build-release.ps1` вызывает [build-bootstrapper.ps1](../scripts/build-bootstrapper.ps1)
после vpk, заменяет внешний Setup на native bundle **до** финализации SHA256SUMS.
Внутренний vendor Setup остаётся в ignored build workspace/bundle и не публикуется
отдельно. Full/Delta/Portable/feed сохраняют прежний Velopack contract.
Каталог закрепляет SDK161, product/version/channel, Setup/full hashes и Update.exe
из Portable того же vpk run. Чужой runtime identity не принимается из IPC.
Unsigned EXE + встроенные hashes дают integrity, **не подтверждение издателя**.
Authenticode/SmartScreen остаются release gates. Текущий unsigned index/footer
расположен в EOF: перед signing необходима reviewed поддержка PE Security Directory
и certificate alignment, иначе добавленная Authenticode certificate table нарушит
поиск footer. Не подписывать этот формат без такой проверки. Использовать PowerShell 7 (`pwsh`).

Изолированная упаковка без изменения обычных outputs:

```powershell
./scripts/build-release.ps1 -Version 0.6.0 -Channel dev -ArtifactDirectory artifacts/bootstrapper/release-proof
./scripts/build-native-bootstrapper.ps1 -Tests
```

Проверенный toolchain: Microsoft Build Tools2022 17.14.41, MSVC19.44.35229
(tools14.44.35207), linker14.44.35229, Windows SDK10.0.26100.0,
Resource Compiler file/product version10.0.26100.7705. Installer exit0,
перезагрузка не требовалась. Minimal Win32 probe: UTF-16 window text, asInvoker/
PerMonitorV2 manifest, exit0. Native product собирается `/W4 /WX`, зависимости только
системные DLL, без MSVC runtime DLL. Source version `.NET10.0.401` не обновлялась.

Исторический прогон до review fixes: restore/build0 errors/warnings; xUnit466/466 (447 Core,
19 WPF), включая25 новых случаев; отдельный Installation/StorageRoot crash filter10/10;
native48 checks — strict JSON/context/stage transitions, Unicode, path rejection,
payload integrity, CreateNew/retry, отмена до и во время extraction, повреждённые index/payload.
Format verify, release-feed tests и изолированный полный packaging успешны.
Packaging сообщает только ожидаемые unsigned warnings; это не compiler warnings.

### Lifecycle final fixes NB-006/NB-007, 12.10.2026

В UI больше нет синхронной записи stdin или ожидания pipe mutex для Close/Cancel/Launch.
Один worker выполняет ordered writes1/2/3/5; Launch публикуется atomic signal один раз.
Close сразу запускает shutdown/watchdog, worker закрывает stdin: прежний helper EOF/CTS
contract отменяет установку без native kill Setup/Minecraft или удаления intent.
Зависший helper может пережить GUI; pinned handles/recovery сохраняют прежний contract.
Начальный outcome1, включая Open→Close/Escape/Cancel без попытки; только typed success0.

Свежие проверки: restore, managed build0 errors/warnings, full486/486, crash/recovery10/10,
native178/180 `/W4 /WX /MT`, format и diff-check,51 local links. В обоих native outputs
прошли11 новых process/window cases плюс прежний retry/hung-helper component:
6 initial/error/success/launch-failure exit cases и5 pipe cases (cancel-blocked,
launch-blocked, EOF, ordered/deduplicated launch, watchdog). Заполненность pipe
подтверждается PeekNamedPipe/реальной quota и незавершённым WriteFile; WM_CLOSE
и UI probe обрабатываются, отменённая запись получает ERROR_OPERATION_ABORTED.
С неотменяемым wait watchdog завершает GUI примерно через5s, child остаётся жив.
Во всех pipe cases sentinel проверяется целиком; assertion failures имеют exit99,
чтобы не выдавать тестовую ошибку за ожидаемый product exit1. Это component/process
regression, не installed GUI E2E, не доказательство поведения зависшего FS driver.

Первый Release full имел10 отсутствующих-host failures: solution собрал TestHost
в Debug; явная Release-сборка Core.Tests/TestHost подготовила DLL, повторный full
и crash прошли без изменения project files. Native suites используют прежние
hash-pinned outer test bundles; неверно выбранный внутренний vendor Setup был
отвергнут (payload absent), затем diagnostic outer прошёл180. Packaging в этом
lifecycle fix не повторялся. EnableLUA=0 не менялся; ordinary-user GUI→Setup,
cross-user/FAT/exFAT/visual/installed update и signing gates остаются обязательными.
До реального GUI E2E PR не объявляется READY TO MERGE; release не разрешён.

### Исторический снимок review fixes NB-001…NB-005, 12.10.2026

Изменения в той же ветке / [Draft PR #14](https://github.com/lDiestrol/minecraft-launcher/pull/14),
без изменения Launcher, guardian, StorageRootResolver, Coordinator и SDK161.

- Staging требует persistent ACL; FAT/exFAT не поддерживаются, fallback отсутствует.
  Проверяются реальные DACL/owner и весь родительский namespace, protected payload ACL,
  no-reparse identity, directory pins без delete-sharing, повторная hash/ACL проверка.
  Payload/dependency и namespace pins передаются helper через explicit HANDLE_LIST
  и удерживаются до его выхода; helper запрещает дальнейшее наследование pins.
  Существующие ACL не меняются. Небезопасный общий namespace отвергается, даже на NTFS.
  Исходный bundle удерживается одним read handle на весь GUI lifetime, retry не
  переоткрывает EXE по имени.
- Close после success/error останавливает reader отдельным shutdown; UI не ждёт
  длительный join. Helper wait ограничен 1s; 5s watchdog может завершить только GUI,
  не installer tree/Minecraft. Intent/locator сохраняются, EOF означает прежний cancel.
  Зависший helper может остаться и блокировать retry до достоверного recovery.
  First Close/Escape во время установки по-прежнему отменяет операцию безопасно.
- Retry получает exit1 до подтверждения; final typed success устанавливает exit0.
  Component test проверяет error → retry/error → retry/success → WM_CLOSE.
- Footer адаптируется к client area, paths/errors прокручиваются, min tracking и
  DPI-change ограничены рабочей областью. Расчёты проверены для100/125/150/200%,
  1366×768 с taskbar и для отрицательных координат второго монитора.
- Diagnostic-only GUID/PID/start UTC ticks связывают process creation evidence и
  `launch-<GUID>.marker.json` schema1 после Main. Уникальный immutable marker не
  заменяет старый файл и не конфликтует с его reader; create acknowledgment не
  является Main confirmation. Harness ждёт matching marker30s без удаления intent.

Свежие результаты: xUnit486/486 (467 Core +19 WPF), **20 новых случаев**:14 marker,
6 pin handoff/validation. Отдельный crash/recovery10/10. Native production177 checks,
diagnostic179 checks (129 общих новых +2 diagnostic identity), плюс отдельный настоящий
Win32 lifecycle component с hung child/WM_CLOSE/intent sentinel. Обе flavours и
component targets собираются `/W4 /WX /MT`. Restore/build0 errors/warnings,
format/diff-check/local links чистые; production и diagnostic packaging изолированы.
Это не GUI→Setup/реальный Launcher E2E. Проверка filesystem flags без ACL — модель,
ACL/rename/symlink/recheck/read-pin inheritance/canary — настоящие Win32 API.
Полная межпользовательская атака и реальный FAT/exFAT остаются VM gates.

### Diagnostic failures и CI без packaging, 17.10.2026

На этапе diagnostic failures `package-diagnostic` **безусловно отключён** в
[ci.yml](../.github/workflows/ci.yml). Возврат packaging/upload требует отдельного
разрешения владельца. Обычный PR CI выполняет .NET tests/format и обе native flavours
`/W4 /WX`, прежние native safety/lifecycle tests и новые diagnostic worker/evidence
regressions. Fixture — data-only MCBOOT01, без runnable helper/Setup; packaged Windows
installer и новый downloadable Windows artifact не создаются. Временные native test
binaries используются только внутри runner. Реальный GUI/Setup E2E не запускается.

Диагностический `--smoke` требует нового evidence leaf под существующим безопасным
parent. Writer сам создаёт protected private leaf, удерживает проверенные предки,
отказывается от existing directory/reparse/collision. Не создавайте каталог evidence
заранее. Для диагностики отказа D: evidence выбирается на отдельном томе/пути,
который проходит ту же строгую проверку предков (например, проверенный C:).
`failure.json` содержит bounded controlled category/stage и typed ACL path/
rule/masks; полный контракт и ограничения — [architecture.md](architecture.md).
Старые screenshots, `launched.json` и Main markers сохраняют назначение; существующие
файлы не перезаписываются. Ошибки записи evidence безопасно поглощаются; short write/
flush может оставить неполный файл. Startup до создания проверенного evidence не
использует `startup-error.log` или иной fallback на непроверенный путь.

Исторически, до новых отчётов, владелец сообщил об успешном C: clean E2E и D: run
`2eb6e67196fb42d2876f5aff153b08a1` с отказом до extraction; точное exception detail
того запуска не сохранено. Предоставленные позднее отчёты домашних Windows10 E2E
сборки `970588dc` документированы 17.10.2026 в
[PROJECT_LEDGER.md](PROJECT_LEDGER.md): C: Positive Clean — PASS, run
`04f7041153da4f9181335a70c224a1c9`; D: Negative ACL — PASS, run
`3ff160b27f824ecc8325a4b83ce2e24d`, `namespace_audit` / `namespace_acl` на `D:\`.
Это внешний результат по отчётам владельца; Debian самостоятельно Windows evidence
не проверял. Diagnostic PASS не закрывает production/recovery/release gates.
Новые E2E и создание diagnostic artifact требуют отдельного разрешения;
старые evidence, Windows ACL и пользовательские данные сохраняются.

### Исторический diagnostic artifact в Windows CI, 14.10.2026

Отдельный `package-diagnostic` в [ci.yml](../.github/workflows/ci.yml) зависит от
успешного build/test/format job. Он доступен только PR #14 из официального репозитория,
head `feat/v061-gui-bootstrapper`, base `feat/v0.6.1`; forks/другие PR и push/main
не выполняют этот job. Checkout использует точный PR head SHA, а не merge commit;
обязательна ancestry проверенного updater fix `ffcebc0`. Windows runner предоставляет
PowerShell7, .NET10, MSVC и Windows SDK; прежние build scripts проверяют C++ tools.

Вызывается только [package-diagnostic-bootstrapper.ps1](../scripts/package-diagnostic-bootstrapper.ps1):
PackId `MinecraftLauncher.BootstrapperDiagnostic`, MainExe `VendorProbe.exe`, 0.0.1/dev,
SDK/vpk строго1.2.161, без signing/release/production packaging. Стандартная vpk
проверка приложения сохраняется: pinned
[CompatUtil161](https://github.com/velopack/velopack/blob/1.2.161/src/vpk/Velopack.Packaging.Windows/CompatUtil.cs)
читает PE/IL и не запускает приложение. Native/Setup/VendorProbe и E2E не запускаются.

[verify-diagnostic-artifact.py](../scripts/verify-diagnostic-artifact.py) статически
проверяет native AMD64 PE, Diagnostic marker/точное совпадение compiled stub, compiled
helper `diagnostic-profile`, MCBOOT01/index/hash каждого embedded файла, pinned
catalog/full-package identity и точное соответствие EXE `SHA256SUMS.txt`.
Verifier покрыт [синтетическими тестами](../tests/DiagnosticArtifactTests.py).
Ошибка до upload завершает job; copied upload files проверяются повторно.
Artifact `minecraft-launcher-diagnostic-<PR-head-SHA>` содержит ровно outer EXE,
`SHA256SUMS.txt`, `build-info.json` (source SHA, verified fix SHA, identity, EXE hash,
Actions run URL), хранится7 дней. Бинарные файлы остаются вне Git. Перед последующим
отдельно согласованным E2E сверить checksum и build-info; unsigned EXE может вызвать
SmartScreen. Packaging/integrity evidence не заменяет реальную Windows установку.

### Историческое состояние Windows E2E, 12–14.10.2026

Диагностический пакет имеет отдельный compiled PackId
`MinecraftLauncher.BootstrapperDiagnostic`, `VendorProbe.exe`, version0.0.1/dev и
Control рядом с **диагностическим** outer EXE (`diagnostic-profile`). Production
helper не имеет переключателя для этой идентичности. Небольшое internal product
binding backend используется только compiled helper/test кодом; default Launcher
identity и публичные API не изменены. Diagnostic host проверяет vendor lifecycle и
launch marker, не Minecraft/WPF startup lifecycle. Vendor shortcuts `None` в этом
пакете: реальные Desktop/StartMenu shortcuts Launcher требуют отдельного smoke.

Сборка [package-diagnostic-bootstrapper.ps1](../scripts/package-diagnostic-bootstrapper.ps1)
успешна. Попытки GUI smoke здесь отклонены **до staging**: host имеет `EnableLUA=0`,
elevated user/Explorer. Защита не отключалась, UAC/registry security не менялись.
Реальная установка C:/D:, vendor TEMP traces, cancellation during Setup, helper/native
crash, retry, HKCU, installed Update.exe и screenshots успешных экранов **не доказаны**.
Владелец согласовал последующий обычный Windows user/VM smoke.

Developer-only [BootstrapperE2E.ps1](../tests/BootstrapperE2E.ps1) требует ordinary
session, проверяет embedded diagnostic catalog до выполнения, отказывается от чужой
diagnostic registration и создаёт уникальные lab paths на выбранных томах. Он
предусматривает clean/retry, Update hash/registration/completion/launch marker,
sentinel GameData/locator, opt-in uninstall/reinstall и screenshots. Script проверен
синтаксически, **его E2E assertions в этой среде не выполнены**. Не использовать
production outer EXE в этом harness. Пользователю обычного installer команды не нужны.
Каждый clean/retry/reinstall использует новый GUID и exact PID/start ticks marker;
старый `launch.marker` не подтверждает запуск. Отдельно проверяется медленный Main
диагностического host, но не вместо полного installed GUI E2E.

```powershell
./scripts/package-diagnostic-bootstrapper.ps1
./tests/BootstrapperE2E.ps1 -Bootstrapper '<diagnostic outer EXE>' -UninstallAndReinstall
```

Тестовый VHDX `D:` не detach/resize; `D:\MinecraftLauncher\GameData`, backing VHDX,
backup и пользовательский locator не изменялись. C: backing файла означает, что D:
не доказывает почти заполненный C:. Disk-full, отказ ACL и detach — только disposable VM.

### План оставшихся Windows GUI E2E: Cancel и Recovery

План подготовлен 17.10.2026 по коду сборки `970588dc`; новые сценарии **не выполнены**.
Статус — **READY FOR CANCEL/RECOVERY TEST PLANNING**. Приоритет P0 означает
необходимое покрытие безопасности данных/recovery, P1 — остальные обязательные
проверки этого плана; безопасный порядок исполнения начинается с простого Cancel.
Результаты двух переданных Windows10 запусков и происхождение сведений — в
[Ledger](PROJECT_LEDGER.md). Все реальные действия ниже требуют отдельного
разрешения владельца: эта задача разрешает документацию, один docs-only commit/push,
но не установки, Cancel/fault injection, ACL/HKCU changes, новую сборку или release.

#### Основания review и общие условия

Проверены [native worker/GUI](../native/Launcher.Bootstrapper/main.cpp),
[extraction](../native/Launcher.Bootstrapper/storage.h),
[helper](../src/Launcher.InstallHelper/Program.cs),
[Coordinator](../src/Launcher.Infrastructure/Installation/InstallationCoordinator.cs),
[backend](../src/Launcher.Infrastructure/Installation/WindowsInstallationBackend.cs),
[intent store](../src/Launcher.Infrastructure/Installation/InstallationIntentStore.cs)
и [Job](../src/Launcher.Infrastructure/Installation/WindowsInstallationJob.cs).
Контракты описаны в [architecture.md](architecture.md); наблюдаемое поведение
реального Velopack после прерывания остаётся предметом будущих E2E.

- Cancel/Close/Escape выставляет native stop/shutdown; extraction проверяет stop
  между файлами и 64 KiB блоками. У helper отмена передаётся EOF stdin → CTS.
  GUI ждёт helper ограниченно; watchdog около 5s завершает только GUI. Исчезновение
  окна не доказывает завершение helper/Job и не разрешает удаление intent.
- Backend при отмене во время ожидания Setup прекращает только созданный installer
  Job. Откат App/HKCU не гарантирован: частичные App, регистрация, locator и pending
  intent могут остаться. Новые Launcher/guardian sessions блокируются pending gate.
- Recover под exclusive lease проверяет owner PID/start time, точную связь
  intent/receipt, пути/package и Job. Живой чужой owner, активный Job, denied/unknown/
  corrupt state блокируют recovery; missing Job допустим только для install-contract1.
  Gate снимается только через Complete после проверки App/HKCU/completion, не по
  таймауту. Crash до completion требует Setup retry; после matching completion и
  всех повторных проверок возможен verified retry без Setup.
- Поздний Cancel может проиграть завершению Setup: после последнего ожидания
  backend не проверяет CTS перед verification/completion. Если успех уже достоверно
  опубликован, полный результат может сохраниться. GUI closing игнорирует события,
  поэтому exit1 сам по себе не доказывает отсутствие completion. Требуется точная
  временная привязка; прогон без попадания в целевую фазу не засчитывать как её PASS.

Будущий стенд: ordinary Windows10 user с UAC, отдельная disposable VM/профиль,
проверенный безопасный NTFS namespace и достаточно места; только уже имеющийся
проверенный compiled Diagnostic artifact с source SHA/build-info/SHA256SUMS.
Старый D: ACL не исправлять ради теста. Production Launcher 0.5.0, исходные пользовательские
GameData, старые evidence и VHDX не использовать для fault injection. Предварительно
сохранить snapshot VM и восстановимую копию тестовых GameData/Control; точные targets
и разрешённые действия согласовать до запуска. Snapshot не заменяет сохранение evidence.

Обязательный комплект **E** для каждого будущего запуска: версия ОС/обычный token,
source SHA и hash outer EXE, product/catalog, уникальный run/launch ID, время действий
и подтверждение достигнутой фазы, GUI/script exit codes с отдельными критериями
safe-script, screenshots/видео, дерево процессов с PID/start time и Job-состоянием,
helper/setup logs при их наличии. До/после — список и SHA-256 тестовых App/Update,
GameData (worlds/settings/user files), locator, intent/receipt/completion и снимки
соответствующих diagnostic и production uninstall keys HKCU (read-only, с типами
значений). Отсутствующие объекты фиксировать явно; результат после GUI exit снимать
также после прекращения установочных writes. Для успешного launch нужны fresh
`launched.json` и Main marker с совпадающими GUID/PID/start ticks; это не требование
к отменённому запуску, где VendorProbe не ожидается. Не публиковать частные пути/данные
из локальных отчётов; существующие evidence не удалять и не перезаписывать.

Ограничения автоматизации: текущий [BootstrapperE2E.ps1](../tests/BootstrapperE2E.ps1)
делает clean→retry и opt-in uninstall/reinstall, но при **начальном** наличии
диагностической регистрации останавливается; это не универсальный safe-script из
переданных отчётов. Он не имеет phase-triggered Cancel/crash режима. Diagnostic
`--smoke` автоматически начинает install, запускает VendorProbe после success и
закрывает GUI после failure: интерактивный Retry в этом режиме не проверяется.
До отдельного согласования harness нельзя считать готовым для сценариев ниже.
Control расположен рядом с diagnostic outer EXE: повтор/recovery должны сохранять
его исходное расположение и прежний Control; перемещение EXE создаст иной профиль.
Evidence leaf для каждого процесса — новый, под существующим проверенным parent;
его не создавать заранее. Hooks/изменение исходников/новая сборка этим планом не разрешены.

#### 1. Cancel до начала распаковки — P1, следующий рекомендуемый тест

| Аспект | План |
| --- | --- |
| Проверяемое поведение | Open→Cancel/Close/Escape до нажатия Install; отдельно Cancel после Install, если наблюдением доказано, что extraction ещё не началась. UI отвечает и закрывается без запуска helper/Setup. |
| Риски файлов и HKCU | В первом варианте ожидается отсутствие install writes. После Install могут успеть появиться выбранный base, `.launcher-install` и пустой private bootstrap leaf; не требовать их автоматического удаления. Ранняя гонка может перейти в extraction — тогда классифицировать как сценарий 2. |
| Предусловия | Общий стенд и E-before; новый target без App/GameData/регистрации. Для ручного Open→Close не использовать auto-install `--smoke`; способ UI-наблюдения согласовать заранее. |
| Ожидаемый результат | GUI exit1; helper/Setup/VendorProbe не запускались, App/GameData/locator/intent/receipt/completion и HKCU не созданы, существующие установки/данные сохранены. Диагностический evidence leaf допустим. |
| Evidence | E + запись момента Cancel и отсутствия process creation/extraction; внешний screenshot до закрытия. `failure.json` не обязателен при закрытии без worker. |
| Автоматизация | Open→Close безопасно автоматизировать UI-средством в VM после разрешения. Узкое окно между Install и extraction нельзя надёжно поймать sleep; без подтверждения фазы оставить этот вариант непроверенным. |
| Отдельное разрешение | Да, на конкретный Windows GUI запуск; вариант после Install также разрешает создание тестового staging. |

#### 2. Cancel во время извлечения payload — P1

| Аспект | План |
| --- | --- |
| Проверяемое поведение | Cancel при фактической записи payload; native worker прекращает extraction и не передаёт неполный payload helper. Проверить Close/Escape как эквивалентные пути отдельно при необходимости. |
| Риски файлов и HKCU | Частичный payload остаётся в уникальном bootstrap staging; cleanup не предусмотрен. Race после extraction может уже запустить helper — такой прогон не доказывает отмену внутри extraction. |
| Предусловия | Сценарий 1, snapshot VM, E-before, разрешённый пустой base. Нужен наблюдаемый write trace/размер незавершённого файла; не замедлять стенд изменением ACL/порчей artifact. |
| Ожидаемый результат | Для доказанной фазы — exit1, только частичный staging, без helper/Setup/VendorProbe, без App/GameData/locator/install records/HKCU changes. Следующая разрешённая попытка использует новый bootstrap GUID, старый staging сохранён. |
| Evidence | E + запись writes до Cancel, границы времени и список частичных файлов после; отсутствие helper/Setup process creation. `failure.json` при наличии имеет extraction stage; отсутствие best-effort evidence не доказывает успех. |
| Автоматизация | Условно: UI Cancel после наблюдаемой записи, затем независимая проверка попадания в фазу. Существующие component extraction tests не заменяют реальный GUI прогон; при слишком быстрой extraction нужен отдельный design harness. |
| Отдельное разрешение | Да, на extraction/Cancel и оставляемые staging files; retry — отдельная явно включённая часть разрешения. |

#### 3. Cancel во время работы Velopack — P0

| Аспект | План |
| --- | --- |
| Проверяемое поведение | Отмена при подтверждённом активном Setup/Job: GUI закрывает IPC, helper получает EOF/CTS, backend прекращает своё installer tree. Зафиксировать завершение helper/Job после GUI exit. |
| Риски файлов и HKCU | Velopack может успеть записать часть App и HKCU. Locator/GameData и intent/receipt уже могут существовать; rollback/удаление не ожидаются. Повторная установка не должна начаться поверх живого Job. |
| Предусловия | Сценарии 1–2 и read-only preservation baseline; disposable VM snapshot, точные diagnostic PID/start times/targets; прежняя production установка исключена из Job. Подготовленный наблюдатель фаз, без process-name-wide kill. |
| Ожидаемый результат | При попадании в активное ожидание Setup — GUI exit1, только owned Job прекращается, автоматического VendorProbe launch и ложного typed success нет; pending gate сохраняет незавершённую операцию. Частичный App/HKCU допускается только как незавершённое состояние. При гонке с verified completion классифицировать результат отдельно по timeline. GameData/production сохранены. |
| Evidence | E + Setup process creation, Job membership/ActiveProcesses до Cancel и после rundown, EOF/cancellation по доступным logs, сохранённые records и HKCU diff. Нельзя выводить Job inactivity только из исчезновения окна/Setup PID. |
| Автоматизация | Только условно безопасна в VM с точной идентификацией процессов и phase observer. Слепой timer и kill по имени не подходят; готового реального Cancel-Setup harness нет. |
| Отдельное разрешение | Да, отдельно на прерывание diagnostic Velopack и возможные частичные App/HKCU writes, затем отдельно на recovery/retry. |

#### 4. Recovery после прерывания установки — P0

| Аспект | План |
| --- | --- |
| Проверяемое поведение | Отдельные варианты: штатный Cancel из 3; crash GUI; crash helper до Setup и при живом installer Job; interruption после Setup до completion и после completion до Complete. Повтор с тем же base/package/Control сначала проверяет lease/owner/Job и связь записей. |
| Риски файлов и HKCU | Установочное дерево может пережить helper благодаря witness; ранний retry опасен. Crash между intent и receipt может оставить несовпадение, которое требует fail-closed остановки. Нельзя вручную удалять/править records, HKCU или «чинить» App для продолжения. |
| Предусловия | Сначала 3, затем отдельный VM snapshot для каждой crash-фазы, точный план fault injection с PID/start time. Старые snapshots/evidence сохранить; без наблюдаемой границы completion узкий crash-вариант не засчитывается. |
| Ожидаемый результат | Живой owner/Job — Busy/отказ без второго Setup; corrupt/unknown/несвязанные records — безопасная остановка. После доказанного rundown и при валидной связке recovery сохраняет gate до verified Complete. До completion повторяет Setup; после matching completion + valid App/HKCU допускает success без Setup. Ни timeout, ни полный App сами по себе не снимают gate. GameData/production сохранены. |
| Evidence | E для прерванной и повторной попыток, процессная/Job timeline, snapshots intent/receipt/completion до/после, Setup count и файлы/HKCU. Подтверждение, что новый Launcher/guardian startup не проходит pending gate; на diagnostic стенде учитывать отдельный профиль, не запускать production Launcher ради этой проверки. |
| Автоматизация | Частично: существующие crash tests используют test host и simulated completion, не реальный Velopack. Реальный recovery требует VM, согласованного phase observer/fault harness; неизвестные metadata оставлять нетронутыми для review. |
| Отдельное разрешение | Да, на каждую crash-фазу и точный процесс, повторную установку и любые test startup проверки. Reboot/power loss — отдельное последующее разрешение, не подразумевается. |

#### 5. Retry после безопасного отказа — P1

| Аспект | План |
| --- | --- |
| Проверяемое поведение | После D:-подобного namespace отказа повтор в том же небезопасном месте снова отказывает. Отдельно — выбор заранее проверенного безопасного C: target без изменения ACL и retry после полностью завершённого старого worker/helper. |
| Риски файлов и HKCU | Ранняя попытка не должна создать App/GameData/HKCU; успешная повторная создаёт только diagnostic установку. Нельзя ослаблять ACL D: или принимать оставшийся staging за доверенный payload. |
| Предусловия | E и отказ с точной категорией; отсутствие другой diagnostic регистрации, новый safe target, сохранённый Control. Для `--smoke` это новый процесс с новым launch ID/evidence; интерактивную кнопку Retry проверять отдельно без auto-close режима. |
| Ожидаемый результат | На прежнем unsafe target — exit1 и тот же безопасный отказ. На safe target — свежий bootstrap GUID, повторная проверка hashes/namespace, success только после App/HKCU/completion, exit0; при разрешённом launch один fresh VendorProbe marker. Предыдущие evidence и production сохранены. |
| Evidence | E для обеих попыток, failure.json, worker/helper rundown, связь выбранных targets с phases, различие staging GUID/launch IDs, Setup/launch counts и полные success records. |
| Автоматизация | Возможно в VM после согласования harness для expected failure/new-process retry. Имеющийся generic E2E script ожидает успех и не реализует такой отрицательный сценарий; UI Retry требует отдельного управления. |
| Отдельное разрешение | Да, на отрицательный повтор и последующую diagnostic установку/launch; изменение ACL не требуется и не разрешается. |

#### 6. Сохранность существующих GameData — P0, обязательна во всех сценариях

| Аспект | План |
| --- | --- |
| Проверяемое поведение | Раздельные случаи configured GameData вне App (включая другой доступный том) и legacy root без locator. Существующие worlds/settings/user files используются без переноса. Непустой неизвестный GameData не принимается автоматически; недоступный configured root не создаёт fallback. |
| Риски файлов и HKCU | Главный риск — ошибочная смена locator/root или повреждение пользовательских файлов при Cancel/retry. Создание locator при закреплении legacy может быть ожидаемо; это не изменение содержимого GameData. Профильные locks/служебные файлы оценивать отдельно от immutable test sentinels. HKCU ограничен diagnostic продуктом. |
| Предусловия | Snapshot и восстановимая копия, только изолированная тестовая копия данных с world/settings/user sentinels, trusted configured/legacy state. Без действующей игры; отказ при активной game/session lock проверять только отдельным согласованным вариантом. Не подключать production данные к diagnostic профилю. |
| Ожидаемый результат | До/после каждой операции те же user-file paths/size/SHA-256, нет удаления/миграции. Configured locator неизменен; для legacy допускается только ожидаемое закрепление прежнего root. No fallback при недоступности; если нужный root не может безопасно использоваться — отказ. Existing production App/HKCU неизменны. |
| Evidence | E + полный manifest immutable user files и diff отдельно от служебных writes, locator bytes/hash, фактический выбранный GameData, production App/HKCU baseline; для refusal — отсутствие второго root и записи в fallback. |
| Автоматизация | Сравнение manifest/locator/HKCU безопасно автоматизировать read-only. Подготовка fixture и операции installer — только в VM; lock/unavailable-volume fault требует отдельного плана, без detach пользовательского D:/VHDX. |
| Отдельное разрешение | Да, на создание копии fixture и реальные install/Cancel/retry; read-only анализ сохранённых отчётов сам по себе установки не разрешает. |

#### 7. Повторный запуск при уже зарегистрированной установке — P1

| Аспект | План |
| --- | --- |
| Проверяемое поведение | A: тот же owned base/Control/package, совпадающие receipt/completion и valid App/HKCU. B: иной registered App, unmanaged legacy App либо несовпадение records. Не смешивать verified repeat с repair/recovery незавершённой операции. |
| Риски файлов и HKCU | Ложное принятие foreign App/registration, повторный Setup поверх живой установки, принятие stale completion. Native extraction повторяется и может создать новый staging даже при verified repeat; это не повторная установка Velopack. |
| Предусловия | Сохранённая successful diagnostic установка, точный прежний outer EXE/Control/base; E-before. Для B — отдельный disposable fixture, без ручной порчи production HKCU/records. Текущий E2E script нельзя повторно запускать вслепую: он останавливается при регистрации. |
| Ожидаемый результат | A: AlreadyInstalled/verified success без нового Setup и HKCU rewrite, с повторной проверкой App/HKCU; при отдельно разрешённом launch один новый VendorProbe marker. Изменение App только в пределах ожидаемого launch marker, GameData/locator сохранены. B: fail-closed, чужой App/HKCU не заменён; при незавершённой owned операции применяются правила 4. |
| Evidence | E + протокольное alreadyInstalled при доступном захвате, независимый process trace об отсутствии Setup, hashes App/Update до/после с выделением нового marker, HKCU/locator/records diff; для B конкретная причина отказа и сохранность foreign установки. |
| Автоматизация | A автоматизируем после согласования безопасного repeat режима с сохранённым Control; нынешний script покрывает repeat внутри собственного clean прогона, не повторный вход со старой регистрацией. B только отдельным fixture; нет разрешения менять HKCU ради подготовки сейчас. |
| Отдельное разрешение | Да, на repeat и optional launch; foreign/legacy fixture/registration setup — отдельное явно указанное разрешение. |

Порядок после согласования: baseline сохранности 6 → Cancel 1 → Cancel 2 → Cancel 3 →
Recovery 4; далее Retry 5 и registered repeat 7. Критерий перехода — отсутствие
необъяснённого diff GameData/production, подтверждённая фаза и достаточные process/
record evidence. При повреждении данных, неизвестном Job/metadata или невозможности
доказать rundown остановить испытание и сохранить всё для review без cleanup.
Первым рекомендуется Open→Cancel до Install: минимальные ожидаемые side effects,
проверка UI/exit1 и наблюдателя сохранности перед испытаниями с реальным Velopack.

### Незакрытые gates и временные файлы

Bootstrapper извлекает крупные файлы на выбранный том; это подтверждено native
extraction tests. Полный OS/vendor write trace не снят. По коду передаётся staging
TEMP, но нет гарантии отсутствия всех иных vendor/OS/LocalAppData записей.
Staging и helper.log намеренно сохраняются для безопасного retry/диагностики;
нет автоматического recursive cleanup, adoption неизвестных файлов или удаления
GameData. Каждый retry создаёт новую extraction directory: накопление места —
явное ограничение этого Draft, managed cleanup требует отдельного reviewed contract.
Uninstall выполняется только vendor updater, GameData/Control не удаляются bootstrapper.

Известный **SDK update download fallback на LocalAppData не исправлен**. Следующий
маленький PR: перед вызовом SDK проверить App/current и packages через безопасные
file handles, volume/path/reparse/write access; при недоступном App fail closed до
download. Проверять actual SDK locator/download paths и отсутствие крупного fallback
при denied packages, недоступном D:, нехватке места, concurrent update/install/game.
До этих fault/E2E проверок и полного Win10/11 Launcher update smoke release запрещён.

Список обязательных обычных Windows10/11 E2E: C/D и Cyrillic/spaces; DPI100/125/150/200
и keyboard/folder dialog; clean + managed retry без второго Setup; legacy/unmanaged
App refusal без миграции; configured root на другом диске/недоступный том; real Launcher
shortcuts/HKCU/Update hash; отмена во время extraction/Setup; helper/native crash +
pending intent recovery; uninstall/reinstall с world/locator hashes; Server/Singleplayer,
guardian и Minecraft после закрытия Launcher; disk-full/ACL/detach в disposable VM.
Дополнительно: real FAT/exFAT rejection до extraction; cross-user ACL/parent substitution;
visual 1366×768/150–200% с taskbar, Tab/Enter/Escape и перенос между мониторами;
GUI/helper hung/driver fault с watchdog, без потери pending state; свежие markers
clean/retry/reinstall с искусственно медленным Main. EnableLUA=0 не обходить.

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
Успех — проверенные installed layout/root, обязательная HKCU-регистрация и durable
completion успешного native Setup после exit0/installer-tree rundown, не предварительный
receipt и не один exit0. Recovery не снимает pending gate до этой проверки. Pending gate блокирует обычный
SDK startup; отдельный install hook допускается только внутри соответствующего Job.

### Исправления ревью PR #13, 12.10.2026

Staging/GameData overlap отвергается до intent/locator, включая equal/ancestor/descendant,
и повторно под lease. Полные файлы без HKCU/completion требуют повторного Setup, не
AlreadyInstalled. Registry verifier сверяет точный PackId/App, manifest title/authors,
major.minor.patch DisplayVersion, точные Update uninstall commands и обязательные поля.
Пользовательские shortcuts необязательны. Чужой registered App получает безопасный отказ.

Velopack161 переименовывает непустой App в App.<random16>; best-effort backup cleanup
может не завершиться. Порядок подтверждён pinned [Setup161 source](https://github.com/velopack/velopack/blob/1.2.161/src/bins/src/commands/install.rs),
поля HKCU — [registry161 source](https://github.com/velopack/velopack/blob/1.2.161/src/bins/src/windows/registry.rs).
Для точно связанных receipt/intent либо receipt/completion retry
доступен с оставшимися обычными siblings. Ни backup, ни чужие siblings не становятся
managed-файлами и не удаляются. Чистая установка остаётся строгой, App.* сам по себе
не доказывает ownership, reparse siblings запрещены.

В новом ignored lab `artifacts/install-review-20261012` создан отдельный diagnostic
PackId `MinecraftLauncher.InstallReview.20261012`, SDK/vpk161, win-x64, dev0.0.1, без
shortcuts. Настоящий Setup запущен production WindowsInstallationJob, helper paused в
install hook после распаковки, но до HKCU. Принудительное завершение installer tree
оставило полный layout без регистрации; повторная авария после vendor rename оставила
rollback siblings. Следующий Setup с открытым no-delete handle тестового backup завершился
exit0, backup остался, production registry verifier принял реальную регистрацию.
Diagnostic uninstall exit0 удалил только diagnostic App/HKCU; retained backups, источники
и logs оставлены. Контрольные world/locator SHA256 неизменны.

Это component/vendor fault-injection, не production Coordinator E2E: diagnostic PackId
не принимается hardcoded Launcher backend. Переход immediately-after-rename не пойман:
повторная авария выполнена позже, в install hook. Реальный ACL отказ записи HKCU не
инъецировался; repeated registration failure покрыт filesystem/injected-registry tests.

### Безопасное обслуживание неопределённых установочных метаданных

Обычный путь — повторить установку тем же доверенным package и canonical base; при живом
owner/Job дождаться фактического завершения, не применять timeout для снятия защиты.
Если receipt/intent/completion отсутствуют, повреждены или противоречат друг другу:

1. Не удалять gate, App, GameData или App.* и не запускать Setup вручную поверх них.
   Сохранить копии Control и установочных logs для диагностики, пользовательские данные
   оставить на месте.
2. Под exclusive installation lease достоверно проверить owner PID/start time, installer
   Job и активные игровые сессии. Denied/unknown запрещает обслуживание.
3. Сверить canonical paths, trusted package hash и идентичность операции с сохранёнными
   достоверными метаданными. Восстановить повреждённую запись только из проверенной копии
   той же операции, затем повторить обычный Coordinator retry. Не создавать completion
   из одного файлового layout/HKCU и не считать имя App.* доказательством владения.
4. Без такой копии/доказательства остановиться для отдельно согласованного обслуживания;
   API не умеет безопасно принимать неизвестный App. Восстановление не должно уничтожать
   данные и не выполняется автоматически этим PR.

Это ограничение crash между отдельными atomic metadata writes, а не обещание общего
transactional rollback. GUI для обслуживания метаданных здесь не реализуется.

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
