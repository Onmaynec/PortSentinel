<div align="center">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.7-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для ETW telemetry, SQLite archive, Installer Watch и explainable network diagnostics.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`. Он не запускает установщики, не перехватывает packet payload и не является malware scanner.

## Что нового в 0.5.7 📦

- новая панель **Installer Watch**;
- Standard Watch: baseline 8 секунд + watch 30 секунд;
- Deep Watch: baseline 10 секунд + watch 60 секунд;
- ручная точка запуска установщика между captures;
- baseline и watch автоматически сохраняются в SQLite;
- optional process hint приоритизирует похожие process names;
- PID-независимый before/after diff новых network fingerprints;
- process candidates, endpoints, TCP/UDP counts и failure signals;
- анализ последней пары архивных captures;
- JSON и Markdown Installer Watch reports;
- Timeline Explorer v0.5.6 полностью сохранён во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| 📦 Installer Watch | Сравнивает baseline и watch во время ручной установки программы |
| 📜 Timeline Explorer | Постранично открывает крупные captures через SQLite |
| 🌐 Network Coverage | Показывает TCP4/TCP6/UDP4/UDP6 protocol families |
| ❤️‍🩹 Connection Health | Анализирует fail/reconnect/retransmit patterns |
| ⚡ ETW Network Capture | Получает read-only kernel network metadata |
| 🛟 Snapshot Fallback | Продолжает работу через Windows IP Helper API без elevation |
| 🗄️ Telemetry Archive | Хранит captures и events в локальной SQLite-базе |
| 🔎 Archive Search | Ищет события по process, IP, note, kind и backend |
| ⇄ Selective Comparison | Сравнивает выбранные capture-сессии без PID |
| 🧹 Retention Center | Выполняет preview и безопасную очистку старого archive |
| 🎯 Baseline & Rules | Показывает deviations, evidence и limitations |
| 🔄 Update Center | Проверяет GitHub Releases и SHA-256 |

## Installer Watch

### Standard Watch

1. Закройте лишние приложения.
2. Укажите optional process hint, например `setup`, `installer` или имя приложения.
3. Запишите 8-секундный baseline.
4. Запустите установщик вручную.
5. Вернитесь в PortSentinel и начните 30-секундный watch capture.
6. Просмотрите process candidates, added metadata и limitations.

### Deep Watch

Deep Watch использует baseline 10 секунд и watch 60 секунд. Он полезен для установщиков, которые загружают несколько компонентов или запускают package manager и service host.

PortSentinel не запускает installer EXE и не изменяет систему.

## Что входит в diff

Installer Watch сравнивает нормализованные network fingerprints:

- event kind;
- protocol family;
- process name;
- remote address и port;
- local binding для listener/accept events.

PID исключён. Для исходящих событий также исключается ephemeral local port, чтобы новый временный порт не создавал ложный уникальный fingerprint.

## Модель доверия

Process hint используется только для сортировки. Он не доказывает, что установщик владеет endpoint.

Новые события могут принадлежать:

- background applications;
- Windows services;
- scheduled tasks;
- child processes установщика;
- service hosts;
- package managers;
- браузеру, открытому установщиком.

Baseline и watch являются отдельными bounded captures. Промежуток между ними не записывается. При SnapshotFallback короткоживущие события и ordering могут отсутствовать.

## Timeline Explorer

Timeline Explorer остаётся доступным во вложенной панели и поддерживает:

- server-side `COUNT(*)`, `LIMIT` и `OFFSET`;
- PageUp/PageDown и Home/End;
- kind/protocol filters;
- parameterized text search;
- переход к sequence number;
- JSON/Markdown export текущей SQL-page.

## ETW coverage

Kernel backend обрабатывает:

- TCP4: connect, accept, disconnect, retransmit, reconnect и fail;
- TCP6: connect, accept, disconnect, retransmit и reconnect;
- UDP4: send и receive;
- UDP6: send и receive.

Kernel ETW control обычно требует запуска от администратора. При недостаточных правах или ошибке logger используется snapshot fallback.

## Privacy boundary

PortSentinel не собирает и не сохраняет packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое.

## Быстрый старт ⚡

1. Скачайте `PortSentinel-0.5.7-win-x64.zip` из Releases.
2. Сверьте архив с `.sha256`.
3. Полностью распакуйте ZIP в отдельную папку.
4. Запустите `portsentinel.exe`.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор меню, процесса или события |
| `Enter` | Начать этап watch или открыть details |
| `E` | Показать added events |
| `L` | Показать limitations |
| `J` / `M` | Экспорт JSON / Markdown |
| `PageUp` / `PageDown` | Переключить страницы Timeline Explorer |
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

- `0.5.x` — automated tests и kernel logger conflict handling;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

---

**PortSentinel 0.5.7** · Windows 10/11 x64 · .NET 8 · MIT
