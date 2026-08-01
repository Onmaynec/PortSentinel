# Changelog

Все значимые изменения PortSentinel фиксируются в этом файле.

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
