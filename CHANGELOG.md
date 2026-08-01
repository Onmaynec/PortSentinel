# Changelog

Все значимые изменения PortSentinel фиксируются в этом файле.

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

### Ограничения

- SQLite history, baseline, ETW, DNS enrichment и Firewall management перенесены в roadmap;
- цифровая подпись executable пока не проверяется;
- metadata protected-процессов может быть ограничена.

## [0.1.0] — 2026-08-01

- первоначальная архитектура и документация проекта.
