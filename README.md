<div align="center">

<img src="assets/logo.svg" width="820" alt="PortSentinel">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.4-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для ETW telemetry, сетевых snapshots, локального archive и объяснимой диагностики соединений.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`, а не оболочка над `netstat` или набор PowerShell-команд.

## Что нового в 0.5.4 ❤️‍🩹

- новая панель **Connection Health**;
- kernel ETW теперь включает `FAIL` и `RECONNECT` events;
- numeric failure code и protocol сохраняются как evidence без speculative decoding;
- Capture & Health выполняет 15-секундный capture и автоматически сохраняет его в SQLite;
- можно анализировать последнюю или выбранную archive capture;
- findings обнаруживают kernel failures, retransmit bursts, reconnect loops и repeated connects;
- каждый finding содержит severity, confidence, evidence и limitation;
- рассчитывается health score 0–100 с уровнями Stable / Observe / Degraded / Critical;
- отчёты экспортируются в JSON и Markdown;
- Archive Operations v0.5.3 полностью сохранён во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| ❤️‍🩹 Connection Health | Анализирует fail/reconnect/retransmit patterns и показывает limitations |
| ⚡ ETW Network Capture | Получает read-only kernel TCP lifecycle metadata |
| 🛟 Snapshot Fallback | Продолжает работу через Windows IP Helper API без elevation |
| ⏱️ Capture Profiles | Запускает capture на 5/15/30/60 секунд |
| 🔎 Archive Search | Ищет события по process, IP, note, kind и backend |
| ⇄ Selective Comparison | Сравнивает выбранную пару capture-сессий без PID |
| 🧹 Retention Center | Показывает dry-run и очищает только старый telemetry archive |
| 👁️ Application Watch | Строит timeline процесса и ищет reconnect loops |
| 🌐 DNS Correlation | Выполняет ограниченный reverse DNS с timeout и кэшем |
| 🎯 Baseline & Rules | Показывает deviations, evidence и limitations |
| 🔄 Update Center | Проверяет GitHub Releases, ZIP и SHA-256 |

## Connection Health

1. Откройте **Capture & Health**.
2. PortSentinel выполнит 15-секундный kernel ETW capture или безопасный snapshot fallback.
3. Результат автоматически сохранится в SQLite.
4. Анализатор сгруппирует health patterns и рассчитает score.
5. Нажмите `Enter`, чтобы открыть evidence и limitation, либо `J`/`M` для экспорта.

Доступны также **Latest Health** и **Archive Health** для повторного анализа сохранённых captures.

## Explainable findings

- `PS-HEALTH-001` — kernel TCP fail events;
- `PS-HEALTH-002` — retransmit burst;
- `PS-HEALTH-003` — repeated reconnects;
- `PS-HEALTH-004` — rapid repeated connections;
- `PS-HEALTH-005` — disconnect без matching connect внутри capture window;
- `PS-HEALTH-006` — limitation snapshot fallback.

Health score не является malware verdict. Retransmits и reconnects могут быть вызваны Wi-Fi, congestion, roaming, proxies, connection pooling или штатной retry logic.

## ETW и fallback

Kernel ETW control обычно требует запуска от администратора. PortSentinel не изменяет системные группы или logger limits. Если права отсутствуют, logger занят или backend возвращает ошибку, используется Windows IP Helper API snapshot.

## Privacy boundary

PortSentinel не собирает и не сохраняет packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое.

## Быстрый старт ⚡

1. Скачайте `PortSentinel-0.5.4-win-x64.zip` из Releases.
2. Сверьте архив с `.sha256`.
3. Полностью распакуйте ZIP в отдельную папку.
4. Запустите `portsentinel.exe`.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта, capture или finding |
| `W` / `S` | Альтернативная навигация в главном меню |
| `Enter` | Открыть экран, evidence или limitation |
| `J` / `M` | Экспорт JSON / Markdown |
| `Y` | Подтвердить retention после preview |
| `Esc` / `Q` | Назад или выход |

## Локальное хранилище

```text
%LocalAppData%\PortSentinel\portsentinel.db
%LocalAppData%\PortSentinel\reports
```

## Командные параметры

```powershell
portsentinel.exe --version
portsentinel.exe --check-update
portsentinel.exe --no-animation
portsentinel.exe --help
```

## Сборка из исходников 🛠️

```powershell
git clone https://github.com/Onmaynec/PortSentinel.git
cd PortSentinel
dotnet build PortSentinel.sln -c Release
dotnet run --project src/PortSentinel/PortSentinel.csproj
```

## Roadmap 🗺️

- `0.5.x` — IPv6/UDP ETW coverage, pagination и automated tests;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

---

**PortSentinel 0.5.4** · Windows 10/11 x64 · .NET 8 · MIT
