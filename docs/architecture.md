# Архитектура Launcher MVP-1

## Проекты и зависимости

`Launcher.Core` — независимое ядро. Здесь находятся `GameProfile`, `LauncherSettings`, bootstrap-модель, правила nickname/RAM/Server URL и небольшие сервисные контракты. Core не знает о WPF, файловой системе и Windows UI.

`Launcher.Infrastructure` зависит только от Core и реализует:

- загрузку bootstrap/profiles через один долгоживущий `HttpClient`;
- JSON parsing и валидацию Server Protocol v1;
- хранение settings в per-user каталоге;
- файловый лог;
- определение физической RAM через Windows API.

`Launcher.App` зависит от Core и Infrastructure. Это WPF presentation layer, ViewModels, команды и composition root в `App.xaml.cs`. `MainWindow.xaml.cs` содержит только `InitializeComponent()`.

```text
Launcher.App ──────────────> Launcher.Core
      │                           ▲
      └──> Launcher.Infrastructure┘
```

Service Locator не используется. Объекты явно создаются composition root и передаются во ViewModel через конструктор.

## Onboarding и подключение

При отсутствии сохранённого URL показывается onboarding. После ввода URL ViewModel:

1. валидирует и нормализует его в Core;
2. получает bootstrap;
3. проверяет `schemaVersion` и обязательные поля;
4. разрешает `profilesUrl` через `System.Uri`;
5. получает и полностью валидирует profiles;
6. сохраняет новый сервер только после полного успеха;
7. переключает UI на основной экран.

Смена сервера проходит тем же путём. Неудачная попытка не уничтожает предыдущие рабочие настройки. Состояния UI — ожидание, получение конфигурации, получение профилей, готово и ошибка; во время HTTP используется indeterminate progress.

## Settings и logging

`JsonSettingsStore` хранит JSON в `%LOCALAPPDATA%\MinecraftLauncher\settings.json` и заменяет файл через временный файл. Отсутствующий, пустой, повреждённый или старый частичный JSON восстанавливается безопасными defaults. ViewModel отдельно проверяет сохранённые RAM и profile id относительно текущей машины и ответа сервера.

`FileAppLogger` пишет startup, shutdown, подключения, HTTP/JSON errors и необработанные исключения в `%LOCALAPPDATA%\MinecraftLauncher\logs`. Ошибка самого логгера не завершает приложение. Пользователь видит короткое сообщение, а stack trace остаётся только в логе.

## Следующие интеграции

- Minecraft runtime/launch: отдельный application service; CmlLib.Core допустимо подключить только на этапе реального запуска.
- Pack updater: загрузка manifest, проверка SHA-256, атомарная синхронизация и Repair поверх сохранённого `ManifestUrl`.
- Self-update: отдельный подписанный release channel; Velopack или другой updater следует оценить тогда, когда появятся installer и release pipeline.

Эти подсистемы не реализованы и не имитируются в MVP-1.
