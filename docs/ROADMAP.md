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
- `NewListenerRule`;
- `WildcardListenerRule`;
- `UnsignedNetworkProcessRule`;
- `TempDirectoryNetworkProcessRule`;
- severity, confidence, evidence и limitations;
- Authenticode metadata;
- SHA-256 enrichment;
- отдельный Rules Center и карточка finding.

## ✅ `0.5.0` — Extended Telemetry

- Application Watch для выбранного процесса;
- snapshot timeline с first seen / last seen / observations;
- connection cycles и reconnect loop detection;
- автоматический JSON/Markdown watch report;
- best-effort DNS correlation с timeout и кэшем;
- Network Process Tree через Toolhelp32;
- session comparison по стабильному fingerprint;
- JSON/Markdown session diff;
- сохранение полного v0.4.0 Control Center.

## `0.5.x` — Telemetry Stabilization

- ETW backend как дополнительный источник событий;
- persistence timeline events в SQLite;
- DNS cache API и более глубокая correlation;
- connection failures и timeout classification;
- installer watch preset;
- выбор произвольных сессий для comparison;
- unit/integration tests для trackers, DNS и process tree;
- фильтры и pagination для больших timeline.

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
