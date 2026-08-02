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

## `0.4.x` — Rule Engine Stabilization

- unit tests для fingerprints и правил;
- фильтры по severity, rule и process;
- экспорт findings в JSON/Markdown;
- allowlist для ожидаемых executable/listeners;
- cache enrichment по path + file metadata;
- улучшенная проверка доверия certificate chain.

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
