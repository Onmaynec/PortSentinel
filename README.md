<div align="center">

<img src="assets/logo.svg" width="820" alt="PortSentinel">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.6-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для масштабируемого telemetry archive, TCP/UDP ETW metadata и explainable diagnostics.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`, а не оболочка над `netstat` или набор PowerShell-команд.

## Что нового в 0.5.6 📜

- новая панель **Timeline Explorer**;
- capture index и event timeline читаются отдельными SQLite-pages;
- крупные captures больше не загружаются целиком для обычного просмотра;
- PageUp/PageDown переключают страницы, Home/End переходят к границам;
- `K` циклически меняет event-kind filter;
- `P` меняет protocol filter между `TCP4`, `TCP6`, `UDP4`, `UDP6`;
- `F` ищет process, IP address, port или diagnostic note;
- `G` переходит к точному sequence number и вычисляет нужную страницу;
- `J`/`M` экспортируют текущую отфильтрованную SQL-page;
- Network Coverage v0.5.5 полностью сохранён во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| 📜 Timeline Explorer | Постранично открывает captures и events без полной materialization |
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

## Timeline Explorer

### Capture Browser

Capture index получает только одну SQL-page за запрос. Размер page автоматически подстраивается под высоту терминала.

- `↑` / `↓` — выбрать capture;
- `PageUp` / `PageDown` — предыдущая или следующая page;
- `Home` / `End` — первая или последняя page;
- `Enter` — открыть timeline выбранной capture.

### Event timeline

Timeline использует отдельный `COUNT(*)` и запрос с `LIMIT/OFFSET`. Фильтры применяются серверной частью SQLite до чтения событий.

- kind presets: connect, accept, disconnect, retransmit, reconnect, fail, UDP send/receive, listener и snapshot;
- protocol presets: `TCP4`, `TCP6`, `UDP4`, `UDP6`;
- text search: process name, local/remote IP, local/remote port и diagnostic note;
- sequence jump: поиск точного event и вычисление его page/index;
- page export: только текущий отображаемый диапазон.

## SQLite indexes и совместимость

Версия 0.5.6 не меняет таблицы и существующие records. Для paged queries добавляются только индексы:

```text
telemetry_events(capture_id, sequence)
telemetry_events(capture_id, kind, sequence)
telemetry_events(capture_id, protocol, sequence)
```

Search text передаётся SQLite через parameters. Символы `%`, `_` и `\` экранируются как literal characters.

## TCP и UDP coverage

Kernel backend обрабатывает:

- TCP4: connect, accept, disconnect, retransmit, reconnect и fail;
- TCP6: connect, accept, disconnect, retransmit и reconnect;
- UDP4: send и receive;
- UDP6: send и receive.

Некоторые UDP callbacks не предоставляют source port. В таком случае недоступный port сохраняется как `0` и отмечается в limitations.

## ETW и fallback

Kernel ETW control обычно требует запуска от администратора. PortSentinel не изменяет системные группы или logger limits. Если права отсутствуют, logger занят или backend возвращает ошибку, используется Windows IP Helper API snapshot.

## Privacy boundary

PortSentinel не собирает и не сохраняет packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое.

## Быстрый старт ⚡

1. Скачайте `PortSentinel-0.5.6-win-x64.zip` из Releases.
2. Сверьте архив с `.sha256`.
3. Полностью распакуйте ZIP в отдельную папку.
4. Запустите `portsentinel.exe`.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор capture или события |
| `PageUp` / `PageDown` | Переключение SQL-pages |
| `Home` / `End` | Первая или последняя page |
| `Enter` | Открыть timeline или event details |
| `K` / `P` | Kind или protocol filter |
| `F` | Text filter |
| `C` | Очистить timeline filters |
| `G` | Перейти к sequence number |
| `J` / `M` | Экспорт текущей page в JSON / Markdown |
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

- `0.5.x` — installer watch preset, automated tests и kernel logger conflict handling;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

---

**PortSentinel 0.5.6** · Windows 10/11 x64 · .NET 8 · MIT
