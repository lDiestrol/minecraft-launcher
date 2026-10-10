# AI handoff: Minecraft Launcher

> Исторический снимок; сохранён до согласованного удаления. Для новой сессии
> используйте [AGENTS.md](../AGENTS.md) и [PROJECT_LEDGER.md](PROJECT_LEDGER.md).
> Путь рабочего ПК и старый baseline ниже не задают путь/ветку другого компьютера.

> Переносимый контекст для новой AI-сессии. Сначала перепроверьте Git и внешнее
> состояние: этот файл описывает подтверждённый снимок на 9 октября 2026 года, а не
> заменяет фактическую диагностику.

## Где находится проект

- Локальный репозиторий на рабочем ПК: `F:\Projects\minecraft-launcher`.
- GitHub: <https://github.com/lDiestrol/minecraft-launcher>.
- Базовая ветка: `main`.
- Снимок baseline: `1ae3ae74b1b70d92031fe8706f80f85e506efd56`.
- Официальный Release: [v0.6.0](https://github.com/lDiestrol/minecraft-launcher/releases/tag/v0.6.0).
- Версия проекта: `0.6.0`; .NET 10, WPF, MVVM, Windows x64.

Если HEAD или production data изменились, используйте новые факты и обновите
документацию отдельным PR.

## С чего начать новую сессию

```powershell
git status
git branch --show-current
git fetch origin
git rev-parse HEAD
git rev-parse origin/main
```

Не выполняйте reset/clean и не перезаписывайте незнакомые изменения. Затем прочитайте:

1. [PROJECT_STATUS.md](PROJECT_STATUS.md);
2. [ROADMAP.md](ROADMAP.md);
3. [architecture.md](architecture.md);
4. [game-launch.md](game-launch.md);
5. [server-protocol.md](server-protocol.md) и [pack-manifest.md](pack-manifest.md);
6. [PACK_MANAGER_PLAN.md](PACK_MANAGER_PLAN.md), только если задача относится к hosting/admin.

## Подтверждённые факты

### Уже реализовано

- onboarding и загрузка server config/profile по HTTPS;
- строгий Server Protocol v1;
- выбор профиля, offline nickname и RAM;
- CmlLib.Core 4.0.6: Minecraft, Fabric, managed Java и запуск процесса;
- Pack Manifest v1, SHA-256 sync/repair, staging и atomic publish;
- серверный Play с автоматическим подключением;
- Dark Gaming WPF UI, адаптивность и WindowChrome;
- Velopack 1.2.158 app updates через GitHub Releases;
- управление папками профиля;
- удаление только managed pack-файлов с сохранением пользовательских данных;
- profile-scoped межпроцессная блокировка Play/Repair/Delete/sync и времени игры;
- release tooling с current-release-only GithubSource feed.

### Проверено пользователем

- установка stable v0.5.0;
- обнаружение, скачивание и применение v0.6.0;
- автоматический restart;
- сохранение settings;
- успешный Minecraft launch после обновления.

### Текущий внешний pack snapshot

WT #1: Minecraft `26.1.2`, Fabric `0.19.3`, packVersion `1.0.1`.
Перед изменением hosting или pack перепроверьте production JSON; этих файлов может не
быть в Git, а значения могли измениться после этого снимка.

## Согласованные требования

### Следующая разработка: только v0.6.1

1. Сохранить основную кнопку серверного запуска.
2. Добавить кнопку «Одиночная игра» для того же профиля и pack sync, но без
   server address/port в launch arguments.
3. Сохранить все существующие locks и lifecycle Minecraft.
4. Сохранить RAM slider и добавить точное числовое поле в МБ с шагом 1 МБ.
5. Не округлять ручной RAM input до 1024 МБ; сохранять точное значение в текущем
   `settings.json`.
6. Улучшить сообщения о блокировках, показать реальный stable/dev channel и сделать
   ошибки понятнее без потери технического лога.

Полные критерии находятся в [ROADMAP.md](ROADMAP.md).

### После v0.6.1

v0.6.2 — отдельный этап расширенного storage management: размер данных, безопасное
удаление profile installation и отдельная очистка общих Minecraft/runtime данных с
учётом других профилей.

Pack Manager и server mod administration — независимый серверный трек. Сначала
publishing core и безопасная миграция, потом WPF Admin или private web UI.

## Что не реализовывать сейчас

- Не начинать v0.6.2 в PR для v0.6.1.
- Не создавать отдельные локальные/offline сборки.
- Не менять Minecraft-сервер, NGINX, DNS или packVersion без отдельной задачи.
- Не переносить pack в LXC без инвентаризации, backup и согласования.
- Не добавлять admin UI, SSH/SFTP или credentials в обычный Launcher.
- Не обновлять другие mods попутно.
- Не менять app version до отдельного release preparation.
- Не создавать tag/Release без явного разрешения.

## Ограничения, которые нельзя нарушать

- Server Protocol v1 и Pack Manifest v1 остаются совместимыми.
- Строгие JSON-схемы не принимают неизвестные поля.
- App update, Minecraft runtime preparation и game pack sync остаются раздельными.
- Не удалять unmanaged mods, worlds, resourcepacks, shaderpacks, screenshots,
  options или другие пользовательские данные без явного разрешения.
- Не следовать через path traversal, symlink, junction или reparse point.
- Не выполнять опасное удаление, sync или launch при конфликтующей операции.
- Не публиковать внутренние IP, hostnames, usernames, SSH keys, tokens или secrets.
- Не автоматизировать stop/restart Minecraft VM, пока lifecycle не исследован.

## Рекомендуемый первый PR v0.6.1

1. Создать feature-ветку от актуального `main`.
2. Исследовать текущий `GameLaunchRequest`, `LauncherOperationCoordinator` и CmlLib
   process options.
3. Сначала добавить Core-модель launch mode и unit tests.
4. Провести server mode через существующий путь без изменения поведения.
5. Добавить singleplayer mode без server endpoint.
6. Интегрировать вторую кнопку в существующий адаптивный UI.
7. Выполнить restore/build/tests/format/diff check и ручной smoke обоих режимов.
8. RAM input лучше делать отдельным следующим PR внутри v0.6.1, если launch mode diff
   становится слишком большим.

Не начинайте реализацию, если рабочее дерево содержит незнакомые изменения или факты
репозитория расходятся с этим документом.

## Обычная проверка проекта

```powershell
dotnet restore MinecraftLauncher.sln
dotnet build MinecraftLauncher.sln
dotnet test MinecraftLauncher.sln
dotnet format MinecraftLauncher.sln --verify-no-changes --no-restore
git diff --check
```

Для чисто документационного PR полная сборка не обязательна; достаточно проверить
Markdown, относительные ссылки и `git diff --check`.

## Идеи и предложения

- Реализовать launch mode как Core enum/value object, а не UI boolean.
- Для RAM считать точное значение authoritative, slider — только одним из input.
- Для v0.6.2 сначала сделать read-only inventory.
- Pack Publisher проектировать как UI-independent core с immutable releases.

Эти идеи требуют code/design review и не должны автоматически расширять scope.

## Вопросы для проверки

- Какие точные CmlLib options отвечают за server quick-connect?
- Как текущая `RamPolicy` формирует min/max и как объяснять clamp в UI?
- Как получить Velopack channel в installed и development mode без догадок?
- Где реально находятся production NGINX files и как выполняется rollback?
- Чем управляется процесс Minecraft-сервера в VM?
- Какие client/server mods должны версионироваться совместно?

## Формат отчёта следующего AI

Всегда разделяйте:

- подтверждённое кодом/командами;
- пользовательски подтверждённое поведение;
- согласованные требования;
- предложения;
- непроверенные внешние факты.
