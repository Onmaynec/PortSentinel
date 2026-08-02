<div align="center">

<img src="assets/logo.svg" width="820" alt="PortSentinel">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.5-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для TCP/UDP ETW telemetry, сетевых snapshots, локального archive и explainable diagnostics.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`, а не оболочка над `netstat` или набор PowerShell-команд.

## Что нового в 0.5.5 🌐

- новая панель **Network Coverage**;
- TCP IPv6 connect, accept, disconnect, retransmit и reconnect events;
- UDP IPv4/IPv6 send и receive events;
- единые protocol families `TCP4`, `TCP6`, `UDP4`, `UDP6`;
- Coverage Capture выполняет 15-секундный capture и автоматически сохраняет его в SQLite;
- доступны Latest Coverage и Archive Coverage;
- protocol matrix показывает events, processes, endpoints и directions;
- отображаются IPv4/IPv6 и TCP/UDP distribution и top remote endpoints;
- coverage reports экспортируются в JSON и Markdown;
- исправлен повторный byte-swap ETW-портов;
- Connection Health v0.5.4 полностью сохранён во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| 🌐 Network Coverage | Показывает наблюдённые TCP4/TCP6/UDP4/UDP6 families |
| ❤️‍🩹 Connection Health | Анализирует fail/reconnect/retransmit patterns |
| ⚡ ETW Network Capture | Получает read-only kernel lifecycle metadata |
| 🛟 Snapshot Fallback | Продолжает работу через Windows IP Helper API без elevation |
| 🗄️ Telemetry Archive | Сохраняет captures и события в SQLite |
| 🔎 Archive Search | Ищет события по process, IP, note, kind и backend |
| ⇄ Selective Comparison | Сравнивает выбранную пару capture-сессий без PID |
| 🧹 Retention Center | Показывает dry-run и очищает только старый archive |
| 👁️ Application Watch | Строит timeline процесса и ищет reconnect loops |
| 🎯 Baseline & Rules | Показывает deviations, evidence и limitations |
| 🔄 Update Center | Проверяет GitHub Releases, ZIP и SHA-256 |

## Network Coverage

1. Откройте **Coverage Capture**.
2. PortSentinel выполнит 15-секундный kernel ETW capture или безопасный snapshot fallback.
3. Capture автоматически сохранится в SQLite.
4. Protocol matrix покажет наблюдённые families, процессы и remote endpoints.
5. `Enter` открывает детали protocol family, `X` — top endpoints, `L` — limitations, `J`/`M` — export.

Coverage описывает только выбранное capture window. Если family не появилась в отчёте, это не доказывает отсутствие соответствующего трафика.

## TCP и UDP coverage

Kernel backend обрабатывает:

- TCP4: connect, accept, disconnect, retransmit, reconnect и fail;
- TCP6: connect, accept, disconnect, retransmit и reconnect;
- UDP4: send и receive;
- UDP6: send и receive.

Некоторые UDP callbacks не предоставляют source port. В таком случае недоступный port сохраняется как `0` и отмечается в limitations.

## ETW и fallback

Kernel ETW control обычно требует запуска от администратора. PortSentinel не изменяет системные группы или logger limits. Если права отсутствуют, logger занят или backend возвращает ошибку, используется Windows IP Helper API snapshot.

Версия 0.5.5 также удаляет повторный byte-swap портов: TraceEvent уже возвращает port values в host byte order.

## Privacy boundary

PortSentinel не собирает и не сохраняет packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое.

## Быстрый старт ⚡

1. Скачайте `PortSentinel-0.5.5-win-x64.zip` из Releases.
2. Сверьте архив с `.sha256`.
3. Полностью распакуйте ZIP в отдельную папку.
4. Запустите `portsentinel.exe`.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта, capture или protocol family |
| `W` / `S` | Альтернативная навигация в главном меню |
| `Enter` | Открыть экран или details |
| `X` | Показать top remote endpoints |
| `L` | Показать coverage limitations |
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

- `0.5.x` — pagination, installer watch, tests и kernel logger conflict handling;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

---

**PortSentinel 0.5.5** · Windows 10/11 x64 · .NET 8 · MIT
