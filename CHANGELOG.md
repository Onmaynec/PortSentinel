# Changelog

Все значимые изменения PortSentinel фиксируются в этом файле.

## [0.5.0] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Extended Telemetry;
- Application Watch для выбранного сетевого процесса;
- timeline first seen / last seen и число наблюдений endpoint;
- подсчёт connection cycles и выделение reconnect loops;
- автоматический экспорт Application Watch в JSON и Markdown;
- best-effort reverse DNS correlation с timeout, ограничением параллелизма и локальным кэшем;
- Network Process Tree через Windows Toolhelp32 API;
- отображение родительских процессов для процессов с сетевой активностью;
- сравнение двух последних сохранённых сессий по стабильному fingerprint;
- diff новых/исчезнувших endpoints и процессов;
- экспорт session diff в JSON и Markdown;
- все возможности v0.4.0 сохранены в отдельном Control Center.

### Безопасность и ограничения

- DNS correlation не считается доказательством владельца трафика;
- Application Watch использует периодические Windows network snapshots и не перехватывает payload;
- reconnect loop определяется по повторным появлениям одинакового process/remote endpoint;
- ETW backend перенесён в стабилизационную ветку 0.5.x и не требуется для запуска v0.5.0.

## [0.4.0] — 2026-08-01

### Добавлено

- новый экран Explainable Rules;
- `NewListenerRule` для listeners, отсутствующих в baseline;
- `WildcardListenerRule` для wildcard bindings;
- `UnsignedNetworkProcessRule` для executable без Authenticode;
- `TempDirectoryNetworkProcessRule` для Temp/Downloads;
- severity, confidence, evidence и limitations для каждого finding;
- SHA-256 enrichment executable;
- чтение Authenticode certificate и publisher;
- подробная карточка finding в TUI.

### Изменено

- baseline сравнивается по стабильному fingerprint, не зависящему от PID;
- главное меню обновлено для v0.4.0;
- README, English README, architecture, roadmap и release notes синхронизированы.

### Безопасность и ограничения

- наличие подписи фиксируется без malware verdict;
- отсутствие Authenticode не считается доказательством вредоносности;
- wildcard listener может быть штатным;
- недоступные executable явно учитываются как ограничение enrichment.

## [0.3.0] — 2026-08-01

### Добавлено

- новая верхнеуровневая TUI-панель Session Intelligence;
- Live Session Recorder с автоматическим сохранением уникальных сетевых записей;
- локальная SQLite-база `%LocalAppData%\PortSentinel\portsentinel.db`;
- WAL journal mode и параметризованные SQL-запросы;
- экран Session History с навигацией стрелками;
- просмотр сохранённых сетевых сессий;
- экспорт сессий в JSON schema v1 и GitHub Markdown;
- Baseline Center с профилем `default`;
- сравнение текущего состояния с baseline;
- отображение новых и исчезнувших сетевых записей;
- Storage Status с путями базы и отчётов;
- Microsoft.Data.Sqlite 8;
- все инструменты v0.2.0 сохранены в разделе Network Tools.

### Безопасность и приватность

- payload, HTTP body и TLS content не сохраняются;
- baseline deviation не считается доказательством угрозы;
- SQLite работает локально без внешнего сервера.

## [0.2.0] — 2026-08-01

### Добавлено

- самостоятельный .NET 8 Windows executable;
- полноэкранный TUI Control Center;
- управление стрелками, Enter, цифровыми клавишами, Esc и Q;
- ASCII-логотип и цветовая тема;
- intro, spinner и progress-анимации;
- TCP/UDP monitoring через Windows IP Helper API;
- IPv4 и IPv6 таблицы;
- сопоставление PID, process name и executable path;
- Live Monitor, Listeners, Connections и Process Details;
- Quick Scan с объяснимыми эвристиками;
- проверка и установка обновлений через GitHub Releases;
- проверка SHA-256 перед установкой;
- GitHub Actions Build и Release;
- русский README по умолчанию;
- MIT License.

## [0.1.0] — 2026-08-01

- первоначальная архитектура и документация проекта.
