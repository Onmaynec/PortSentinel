# Changelog

Все значимые изменения PortSentinel фиксируются в этом файле.

## [0.5.7] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Installer Watch;
- Standard Watch: 8-секундный baseline и 30-секундный watch capture;
- Deep Watch: 10-секундный baseline и 60-секундный watch capture;
- ручная точка старта установщика между двумя capture-сессиями;
- автоматическое сохранение baseline и watch captures в существующий SQLite archive;
- анализ любой последней пары архивных captures;
- optional process hint для приоритизации process candidates;
- PID-независимое before/after сравнение network fingerprints;
- исключение outbound ephemeral local port из fingerprint для снижения ожидаемого шума;
- группировка новых events по процессу, TCP/UDP family, endpoints и failure signals;
- просмотр added metadata, process candidates и limitations в TUI;
- JSON schema v1 и Markdown export Installer Watch reports;
- полный Timeline Explorer v0.5.6 сохранён во вложенной панели.

### Модель доверия и безопасность

- PortSentinel не запускает installer EXE и не изменяет систему;
- process hint является только приоритетом отображения, а не доказательством attribution;
- новые events могут принадлежать background applications, services или scheduled tasks;
- установщик может делегировать трафик child processes, service hosts, package managers или browser;
- baseline и watch являются отдельными bounded captures, поэтому промежуток между ними не записывается;
- packet payload, HTTP body, cookies, credentials, tokens и decrypted TLS content не собираются.

## [0.5.6] — 2026-08-02

- Timeline Explorer с server-side SQLite pagination через `COUNT(*)`, `LIMIT` и `OFFSET`;
- PageUp/PageDown, Home/End, kind/protocol filters, parameterized text search и sequence jump;
- JSON/Markdown export текущей SQL-page;
- backward-compatible indexes по capture/sequence, capture/kind и capture/protocol.

## [0.5.5] — 2026-08-02

- TCP IPv6 lifecycle callbacks и UDP IPv4/IPv6 send/receive callbacks;
- protocol families `TCP4`, `TCP6`, `UDP4`, `UDP6`;
- Network Coverage reports, protocol matrix и top endpoints;
- исправлен повторный byte-swap ETW-портов.

## [0.5.4] — 2026-08-02

- kernel `TcpIpFail` и `TcpIpReconnect` metadata;
- explainable Connection Health findings и score 0–100;
- live/latest/archive health reports с evidence и limitations.

## [0.5.3] — 2026-08-02

- capture profiles 5/15/30/60 секунд;
- parameterized archive search и selective comparison;
- retention preview и транзакционная cascade cleanup.

## [0.5.2] — 2026-08-02

- SQLite persistence ETW/fallback captures;
- Telemetry History, event viewer и capture comparison;
- JSON/Markdown archive exports.

## [0.5.1] — 2026-08-02

- read-only kernel ETW backend через TraceEvent;
- TCP IPv4 connect/accept/disconnect/retransmit;
- capability probe, bounded capture и snapshot fallback.

## [0.5.0] — 2026-08-02

- Application Watch, reverse DNS correlation и Network Process Tree;
- session comparison и exports;
- полный предыдущий Control Center.

## [0.4.0] — 2026-08-01

- explainable rules, stable baseline fingerprint без PID;
- severity, confidence, evidence и limitations;
- Authenticode и SHA-256 enrichment.

## [0.3.0] — 2026-08-01

- SQLite sessions и WAL;
- Live Session Recorder, history, reports и Baseline Center.

## [0.2.0] — 2026-08-01

- self-contained Windows TUI;
- TCP/UDP IPv4/IPv6 через Windows IP Helper API;
- process mapping, Quick Scan и GitHub Releases updater.

## [0.1.0] — 2026-08-01

- первоначальная архитектура и документация проекта.
