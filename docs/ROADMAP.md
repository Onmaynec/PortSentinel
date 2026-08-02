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
- `0.5.7` — Installer Watch: guided baseline/watch captures и explainable before/after reports.

## ✅ `0.5.8` — ETW Session Guard

- preflight inventory активных ETW session names;
- ownership boundary `PortSentinel-*` / foreign;
- guarded 15-second capture с SQLite persistence;
- attempt, backend, fallback, native-code и session-count diagnostics;
- best-effort failure classification;
- bounded retry только при вероятном name collision;
- JSON/Markdown inventory и diagnostics reports;
- dry-run cleanup собственных orphan sessions;
- обязательное подтверждение `Y`;
- foreign sessions никогда не изменяются.

## `0.5.x` — Telemetry Stabilization

- unit/integration tests для ETW mapping, session guard, coverage, health, archive, timeline и installer watch;
- документированная mapping table известных failure codes только при наличии authoritative источника;
- regression fixtures для SQLite schema и report serialization;
- deterministic fixtures для snapshot fallback и report exports.

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
5. Слить release PR в `main`.
6. Workflow **Release** создаст ZIP, SHA-256, тег и Release.
7. Проверить тег, release assets и обновление из предыдущей версии.
8. Удалить слитую release-ветку.

Подробности: [`UPDATES.md`](UPDATES.md).
