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

### Windows E2E — не завершён

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
