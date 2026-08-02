# 🗺️ Roadmap PortSentinel

Каждая версия выпускается по одной схеме: рабочий vertical slice → Windows x64 Build → portable ZIP → SHA-256 → GitHub Release → поддержка встроенного updater.

## ✅ `0.2.0` — Network Control Center

- self-contained Windows executable;
- полноэкранная TUI;
- TCP/UDP IPv4/IPv6 через Windows IP Helper API;
- process mapping, listeners, connections, Quick Scan и updater.

## ✅ `0.3.0` — Sessions & Reports

- SQLite session storage и WAL;
- Live Session Recorder, history, exports и baseline.

## ✅ `0.4.0` — Explainable Rules

- стабильный fingerprint без PID;
- deterministic rules, evidence, confidence и limitations;
- Authenticode и SHA-256 enrichment.

## ✅ `0.5.0` — Extended Telemetry

- Application Watch, DNS correlation, process tree и session comparison.

## ✅ `0.5.1` — ETW Telemetry

- read-only kernel ETW TCP IPv4 lifecycle events;
- capability probe, bounded capture, reports и snapshot fallback.

## ✅ `0.5.2` — Telemetry Archive

- SQLite persistence, history, event viewer и lifecycle comparison.

## ✅ `0.5.3` — Archive Operations

- capture profiles, parameterized search, selective comparison и retention preview.

## ✅ `0.5.4` — Connection Health

- kernel fail/reconnect events;
- explainable health findings и score;
- live/archive reports.

## ✅ `0.5.5` — Network Coverage

- TCP IPv6 connect/accept/disconnect/retransmit/reconnect callbacks;
- UDP IPv4/IPv6 send/receive callbacks;
- normalized `TCP4`, `TCP6`, `UDP4`, `UDP6` families;
- live/latest/archive coverage reports;
- protocol matrix, IP-family distribution и top endpoints;
- JSON/Markdown exports;
- corrected ETW port byte-order handling;
- explicit UDP source-port и bounded-window limitations.

## `0.5.x` — Telemetry Stabilization

- pagination очень больших timeline;
- installer watch preset;
- unit/integration tests для ETW mapping, coverage, health, archive, trackers, DNS и process tree;
- обработка simultaneous kernel logger conflicts;
- документированная mapping table для известных failure codes только при наличии authoritative источника.

## `0.6.0` — Managed Firewall

- read-only Firewall correlation;
- изменение только PortSentinel-managed rules;
- plan, dry-run и явное подтверждение;
- transaction journal и rollback;
- защита от удаления сторонних правил.

## `1.0.0` — стабильный продукт

- стабильные config/report schemas;
- подписанные релизы;
- проверенный updater и rollback;
- backward compatibility;
- unit/integration tests;
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
