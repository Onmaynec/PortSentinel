# 🗺️ Roadmap PortSentinel

Roadmap отражает целевое направление и может меняться после проверки первого vertical slice.

## `0.1.0` — надёжное ядро

- CLI и базовый TUI;
- TCP/UDP, IPv4/IPv6;
- process mapping и process lifecycle;
- listeners и active connections;
- sessions и SQLite;
- signature verification и SHA-256;
- baseline;
- первые explainable rules;
- quickscan;
- console/JSON/HTML reports;
- Firewall read model, plan, managed allow/block и rollback;
- privacy modes, portable mode, RU/EN;
- tests, CI, portable ZIP и SHA-256 checksum.

## `0.2.0` — расширенная телеметрия

- ETW backend;
- DNS correlation;
- process tree;
- installer mode;
- connection failures;
- timeline.

## `0.3.0` — анализ и сравнение

- offline enrichment;
- session comparison;
- application profiles;
- custom rule packs;
- advanced charts.

## `0.4.0` — расширяемость

- Source SDK;
- Rule SDK;
- plugins;
- signed rule packs;
- diagnostic bundles.

## `1.0.0` — стабильный продукт

- stable schemas;
- stable source/rule APIs;
- optimized storage;
- signed releases;
- verified Firewall rollback;
- backward compatibility.

## Порядок реализации

1. Domain models, source interfaces, identities, session lifecycle и storage schema.
2. Solution skeleton, DI, logging, config, CLI и migrations.
3. Первый vertical slice: TCP source → process mapping → listener detection → console → SQLite.
4. Signatures и hashing.
5. UDP.
6. Rule Engine.
7. Baselines.
8. Firewall read/plan/managed actions/rollback.
9. Reports и TUI.
10. ETW, DNS и installer mode.
11. Tests, CI, localization, docs и portable release.

После каждого этапа проект должен собираться и иметь рабочий end-to-end сценарий.
