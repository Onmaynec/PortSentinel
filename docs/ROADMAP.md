# 🗺️ Roadmap PortSentinel

Каждая версия выпускается по одной схеме: рабочий vertical slice → Windows x64 Build → portable ZIP → SHA-256 → GitHub Release → поддержка встроенного updater.

## ✅ Выпущенные vertical slices

- `0.2.0` — Network Control Center: Windows TUI, TCP/UDP IPv4/IPv6 snapshots, process mapping и updater.
- `0.3.0` — Sessions & Reports: SQLite, history, exports и baselines.
- `0.4.0` — Explainable Rules: stable fingerprints, evidence, confidence, Authenticode и SHA-256.
- `0.5.0` — Extended Telemetry: Application Watch, DNS correlation, process tree и session comparison.
- `0.5.1` — ETW Telemetry: read-only kernel TCP lifecycle events и snapshot fallback.
- `0.5.2` — Telemetry Archive: persistence, history, event viewer и capture comparison.
- `0.5.3` — Archive Operations: capture profiles, search, selective comparison и retention preview.
- `0.5.4` — Connection Health: fail/reconnect metadata, explainable findings и score.
- `0.5.5` — Network Coverage: TCP6, UDP4/UDP6, protocol matrix и corrected port handling.
- `0.5.6` — Timeline Explorer: server-side pagination, filters, sequence jump и page exports.

## ✅ `0.5.7` — Installer Watch

- Standard Watch: baseline 8 секунд + watch 30 секунд;
- Deep Watch: baseline 10 секунд + watch 60 секунд;
- ручная точка старта installer EXE;
- автоматическое архивирование обеих captures;
- optional process hint без attribution verdict;
- PID-независимые before/after fingerprints;
- process candidates, endpoints, protocol counts и failure signals;
- latest-pair analysis;
- JSON/Markdown reports;
- explicit background, child-process, bounded-window и fallback limitations.

## `0.5.x` — Telemetry Stabilization

- unit/integration tests для ETW mapping, coverage, health, archive, timeline и installer watch;
- обработка simultaneous kernel logger conflicts;
- документированная mapping table известных failure codes только при наличии authoritative источника;
- regression fixtures для SQLite schema и report serialization.

## `0.6.0` — Managed Firewall

- read-only Windows Firewall correlation;
- изменение только PortSentinel-managed rules;
- plan, dry-run и явное подтверждение;
- transaction journal и rollback;
- защита сторонних правил от изменения или удаления.

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
