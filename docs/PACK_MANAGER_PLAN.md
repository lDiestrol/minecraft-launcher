# План серверного Pack Manager

> Design-документ. Здесь зафиксированы согласованные цели и безопасная
> последовательность исследования. Ни LXC, ни Publisher, ни админ-интерфейс пока
> не считаются реализованными.

## Цель

Убрать зависимость хранения и ручной публикации клиентских модпаков от текущего
файлового дерева NGINX, сохранив совместимость существующего Launcher v0.6.0,
публичные URL и строгие контракты Server Protocol v1 / Pack Manifest v1.

## Подтверждённые факты

- Launcher получает `bootstrap.json`, `profiles.json`, `manifest.json` и pack-файлы
  по HTTP(S); текущий production hosting обслуживает NGINX.
- Pack Manifest v1 перечисляет Launcher-managed `mods/` и `config/` с SHA-256,
  размером и URL.
- JSON schema строгая: неизвестные поля отклоняются.
- Текущий клиент не требует динамического backend и должен продолжить работать со
  статическими ответами.
- Minecraft-сервер работает в отдельной VM; серверные моды физически остаются там.
- Точный способ запуска сервера — systemd service или ручной запуск — должен быть
  установлен перед автоматизацией lifecycle.
- Production пути NGINX, права, источники JSON и процедура текущей публикации ещё
  не зафиксированы в публичном репозитории.
- В публичном репозитории не должно быть внутренних IP, SSH-параметров, ключей,
  токенов или других секретов домашней инфраструктуры.

## Согласованные требования

### Целевая инфраструктура клиентских pack

- Выделить лёгкий LXC-контейнер на существующем Proxmox, без отдельной VM для
  Pack Manager или веб-панели.
- Хранить в LXC `bootstrap.json`, `profiles.json`, `manifest.json` и файлы
  клиентских модпаков.
- Оставить NGINX HTTPS reverse proxy перед хранилищем/сервисом.
- По возможности сохранить существующие публичные URL без redirect или изменения
  server config в уже установленных Launcher.
- Гарантировать работу Launcher v0.6.0 без обновления клиента.
- Не менять Server Protocol v1 и Pack Manifest v1 и не добавлять поля в строгий JSON.

### Pack Publisher

Publisher должен поддерживать:

- создание draft версии клиентского pack;
- загрузку и инвентаризацию файлов;
- вычисление фактических SHA-256 и размеров;
- валидацию path, mod duplicates и согласованности profile/manifest;
- явную версию pack;
- неизменяемые опубликованные версии;
- atomic publication: клиент видит либо старый, либо полностью готовый новый pack;
- журнал действий и историю опубликованных версий;
- rollback на ранее проверенную версию без перезаписи её содержимого;
- предварительный просмотр diff до публикации;
- отказ при неполном staging, hash mismatch или нарушении schema.

Publisher не должен автоматически менять Launcher application version или GitHub
Release. Application update и game pack publication остаются независимыми.

### Управление модами Minecraft-сервера

На отдельном этапе требуется:

- подключение к серверной VM по SSH/SFTP с минимально необходимыми правами;
- отдельная версия server pack;
- получение фактического списка, размеров и SHA-256 установленных серверных файлов;
- загрузка изменений только во временный staging-каталог;
- резервная копия изменяемых файлов и metadata до применения;
- проверка staged content до замены production-файлов;
- явное подтверждение остановки и перезапуска сервера;
- rollback при неуспешном запуске или ручном решении администратора;
- согласованный release plan для client-only, server-only и common модов.

Если сервер запускается вручную или lifecycle нельзя надёжно определить, система
может подготовить staging и инструкции, но не должна автоматически останавливать,
запускать или убивать процесс.

Постоянный агент на Minecraft VM на первом этапе не нужен. Возможный systemd timer
для read-only проверки обновлений рассматривается только позже.

### Административные интерфейсы

Допустимы два клиента общего publishing core:

1. отдельный Windows Minecraft Launcher Admin на WPF с SSH/SFTP;
2. небольшая локальная web-панель в том же LXC.

Сначала должно появиться общее серверное ядро публикации и API/command boundary,
затем UI. Web-панель не публикуется напрямую в интернет: обязательны авторизация,
сетевое ограничение доступа, TLS на доверенной границе и минимальные privileges.

Обычный пользовательский Launcher не получает admin endpoints, SSH-клиенты или
секреты.

### Безопасность данных

- Никаких рекурсивных удалений по непроверенным или полученным от клиента путям.
- Staging, backups и published roots должны быть отдельными проверенными каталогами.
- Symlink/junction traversal и выход за root запрещаются.
- Published content считается immutable; исправление выпускается новой версией.
- Секреты хранятся вне Git, pack JSON и client binaries.
- Логи не должны содержать credentials, приватные ключи или полные чувствительные URL.
- Операции publish, rollback, server apply и cleanup должны иметь отдельные права.

## Предлагаемая последовательность миграции

Эти этапы требуют отдельного согласования перед действиями над production.

### Этап 0 — инвентаризация

1. Найти фактические NGINX roots, server blocks и public URL.
2. Скопировать текущие `bootstrap.json`, `profiles.json`, manifest и payload inventory.
3. Проверить SHA-256 всех опубликованных файлов и managed mod count.
4. Зафиксировать ownership/permissions, размер данных, backup и rollback procedure.
5. Определить, какие файлы создаются вручную и какой источник является authoritative.

### Этап 1 — модель и локальный Publisher

1. Описать draft/published/history storage model.
2. Реализовать offline validation и generation существующих JSON v1.
3. Добавить тесты path safety, duplicate paths/mod ids, hashes, interrupted publish и rollback.
4. Прогнать Publisher на копии production data без сетевых изменений.

### Этап 2 — LXC и shadow hosting

1. Подготовить минимальный LXC, storage, service account и backups.
2. Развернуть Publisher и read-only endpoint во внутренней сети.
3. Импортировать verified snapshot.
4. Сравнить ответы и байты файлов с текущим NGINX hosting.
5. Проверить Launcher v0.6.0 через временный внутренний endpoint.

### Этап 3 — безопасное переключение

1. Создать актуальный backup и зафиксировать rollback point.
2. Настроить NGINX reverse proxy без изменения публичных URL.
3. Проверить TLS, caching, content types, ranges и download limits.
4. Выполнить smoke test bootstrap → profiles → manifest → pack files.
5. Наблюдать ошибки и сохранить немедленный rollback на старый static root.

### Этап 4 — server pack coordination

Только после стабильной клиентской публикации добавить read-only server inventory,
затем staging/backup, и лишь после этого управляемое apply/restart с подтверждением.

## Идеи и предложения

Следующие решения ещё не согласованы:

- content-addressed blob storage для дедупликации при сохранении immutable releases;
- manifest signing поверх текущего v1 без изменения JSON клиента, например через
  отдельный deployment artifact;
- REST API против локального CLI/worker queue;
- WPF Admin как первый UI либо web UI как первый UI;
- автоматическое определение client/server/common модов по metadata;
- retention policy для draft, staging и backups;
- health check Minecraft-сервера после restart;
- плановая read-only проверка через systemd timer.

## Вопросы для проверки

### Текущий NGINX и данные

- Где находятся реальные document roots и конфигурация reverse proxy?
- Какие URL сейчас записаны в production bootstrap/profiles/manifest?
- Есть ли CDN/cache и как выполняется invalidation?
- Кто владеет файлами и какими командами выполняется текущая публикация?
- Где и сколько хранятся резервные копии?

### Proxmox и LXC

- Какая ОС, storage quota, backup schedule и recovery target доступны LXC?
- Какой внутренний DNS/hostname может использовать NGINX?
- Какие входящие/исходящие соединения разрешены firewall?
- Где хранить service credentials и кто имеет административный доступ?

### Minecraft VM

- Сервер действительно управляется systemd, wrapper script или вручную?
- Какой каталог содержит mods/config и какие файлы считаются пользовательскими?
- Как проверять готовность после запуска и какой timeout допустим?
- Какие моды должны совпадать на клиенте и сервере, а какие являются side-specific?
- Какой объём backup нужен и сколько версий сохранять?

### Publisher

- Какой компонент является authoritative для pack version?
- Нужны ли роли reviewer/publisher отдельно?
- Какая атомарная primitive доступна на выбранном storage?
- Требуется ли audit log с экспортом или достаточно локального append-only журнала?

## Не выполнять без отдельного разрешения

- Не переносить production data и не менять NGINX/DNS.
- Не подключаться к Minecraft VM и не менять серверные моды.
- Не создавать LXC или admin credentials.
- Не останавливать и не перезапускать Minecraft-сервер.
- Не добавлять административные возможности в обычный Launcher.
