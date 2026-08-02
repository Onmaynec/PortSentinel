# 🗺️ Roadmap PortSentinel

Каждая версия выпускается по одной схеме: рабочий vertical slice → Windows x64 Build → portable ZIP → SHA-256 → GitHub Release → поддержка встроенного updater.

## ✅ `0.2.0` — Network Control Center

- self-contained Windows executable;
- полноэкранная TUI;
- TCP/UDP IPv4/IPv6 через Windows IP Helper API;
- process mapping, listeners, connections, Quick Scan и updater.

## ✅ `0.3.0` — Sessions & Reports

- SQLite session storage и WAL;
- Live Session Recorder;
- Session History;
- JSON/Markdown exports;
- Baseline Center;
- portable data directory.

## ✅ `0.4.0` — Baseline & Explainable Rules

- стабильный baseline fingerprint без зависимости от PID;
- explainable rules;
- severity, confidence, evidence и limitations;
- Authenticode metadata и SHA-256 enrichment.

## ✅ `0.5.0` — Extended Telemetry

- Application Watch и reconnect-loop detection;
- reverse DNS correlation;
- Network Process Tree;
- session comparison и exports;
- полный предыдущий Control Center.

## ✅ `0.5.1` — ETW Telemetry

- read-only kernel ETW backend через TraceEvent;
- TCP IPv4 connect/accept/disconnect/retransmit events;
- capability probe и elevated-access status;
- ограниченное capture window;
- JSON/Markdown ETW reports;
- автоматический snapshot fallback;
- явная privacy boundary без packet payload.

## ✅ `0.5.2` — Telemetry Archive

- persistence ETW и fallback captures в SQLite;
- транзакционное сохранение capture headers и events;
- Telemetry History;
- просмотр и export сохранённых событий;
- lifecycle fingerprint без PID;
- comparison двух последних captures;
- JSON/Markdown telemetry diff;
- backward-compatible schema extension.

## `0.5.x` — Telemetry Stabilization

- IPv6 и UDP provider coverage;
- connection failures и timeout classification;
- выбор длительности capture;
- installer watch preset;
- выбор произвольных captures для comparison;
- retention и очистка старых archive records;
- фильтры и pagination больших timeline;
- unit/integration tests для ETW mapping, archive, trackers, DNS и process tree;
- обработка simultaneous kernel logger conflicts.

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
