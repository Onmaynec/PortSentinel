# 🗺️ Roadmap PortSentinel

Каждая версия выпускается по одной схеме: рабочий vertical slice → Windows x64 Build → portable ZIP → SHA-256 → GitHub Release → поддержка встроенного updater.

## ✅ `0.2.0` — Network Control Center

Выполнено:

- самостоятельный self-contained `portsentinel.exe`;
- полноэкранная русская TUI-панель;
- навигация стрелками, Enter и цифровыми клавишами;
- ASCII-логотип, заставка, spinner и progress-анимации;
- TCP/UDP IPv4/IPv6 через Windows IP Helper API;
- PID, process name и executable path mapping;
- Live Monitor, Listening Ports и Active Connections;
- Process Inspector;
- Quick Scan с explainable findings;
- GitHub Releases updater с проверкой SHA-256;
- Build и Release workflows;
- MIT License и русская документация по умолчанию.

## `0.2.x` — стабилизация интерфейса

- интерактивная сортировка таблиц;
- фильтры по процессу, PID, протоколу, порту и адресу;
- поиск внутри Process Inspector;
- настройка частоты обновления;
- темы и дополнительные визуальные эффекты без перегрузки интерфейса;
- улучшенная обработка изменения размера терминала;
- smoke tests интерфейса и native adapters.

## `0.3.0` — Sessions & Reports

- SQLite session storage;
- история запусков;
- connection lifecycle между снимками;
- экспорт JSON и Markdown;
- автономный HTML report;
- data-quality блок и dropped/limited metadata counters;
- retention и portable data directory.

## `0.4.0` — Baseline & Rules

- создание baseline обычной активности;
- сравнение нового снимка и сессии с baseline;
- `NewListenerRule`;
- `WildcardListenerRule`;
- `UnsignedNetworkProcessRule`;
- `TempDirectoryNetworkProcessRule`;
- severity, confidence, evidence и limitations;
- digital signature и SHA-256 enrichment.

## `0.5.0` — Extended Telemetry

- ETW backend;
- DNS correlation;
- process tree;
- connection failures и reconnect loops;
- installer/application watch modes;
- timeline и session comparison.

## `0.6.0` — Managed Firewall

- read-only Firewall correlation;
- изменение только PortSentinel-managed rules;
- plan и dry-run;
- явное подтверждение;
- transaction journal;
- rollback;
- защита от удаления сторонних правил.

## `1.0.0` — стабильный продукт

- стабильные config/report schemas;
- подписанные релизы;
- проверенный updater и rollback;
- backward compatibility;
- unit/integration tests для native networking, storage и rules;
- документированная privacy/security model.

## Правило выпуска следующей версии

1. Реализовать рабочий end-to-end сценарий.
2. Обновить версию в `.csproj`, `VERSION`, README и CHANGELOG.
3. Обновить `RELEASE_NOTES.md`.
4. Проверить PR через workflow **Build**.
5. Последним изменить `VERSION` и слить release PR.
6. Workflow **Release** создаст ZIP, SHA-256, тег и Release.
7. Проверить обновление из предыдущей версии через **Update Center**.

Подробности: [`UPDATES.md`](UPDATES.md).
