<div align="center">

<img src="assets/logo.svg" width="820" alt="PortSentinel">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.3-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для ETW-событий, сетевых snapshots, локального telemetry archive и объяснимого анализа.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`, а не оболочка над `netstat` или набор PowerShell-команд.

## Что нового в 0.5.3 🧰

- добавлена верхнеуровневая панель **Archive Operations**;
- доступны capture profiles на 5, 15, 30 и 60 секунд;
- каждый profile capture автоматически сохраняется в SQLite;
- Archive Search ищет по process name, IP addresses и diagnostic notes;
- добавлены preset-фильтры retransmit, disconnect, fallback и listeners;
- Selective Comparison позволяет выбрать любую пару из последних 50 captures;
- Retention Center показывает размер archive и dry-run удаления;
- можно сохранить последние 25, 50, 100 или 250 captures;
- удаление запускается только после явного подтверждения `Y`;
- полный Telemetry Archive v0.5.2 сохранён во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| ⏱️ Capture Profiles | Запускает ETW/fallback capture на 5/15/30/60 секунд |
| 🔎 Archive Search | Ищет события по процессу, IP, note, kind и backend |
| ⇄ Selective Comparison | Сравнивает выбранную пару capture-сессий |
| 🧹 Retention Center | Показывает dry-run и очищает только старый telemetry archive |
| 🗄️ Telemetry Archive | Сохраняет ETW/fallback captures и открывает history |
| ⚡ ETW Network Capture | Получает kernel TCP lifecycle events без packet payload |
| 🛟 Snapshot Fallback | Продолжает работу через Windows IP Helper API без ETW |
| 👁️ Application Watch | Строит timeline процесса и ищет reconnect loops |
| 🌐 DNS Correlation | Выполняет ограниченный reverse DNS с timeout и кэшем |
| 🎯 Baseline & Rules | Показывает deviations, evidence и limitations |
| 🔄 Update Center | Проверяет GitHub Releases, ZIP и SHA-256 |

## Capture Profiles

Профили ограничивают длительность capture и не меняют backend:

- **Quick** — 5 секунд;
- **Standard** — 15 секунд;
- **Deep** — 30 секунд;
- **Investigator** — 60 секунд.

При наличии elevated access используется kernel ETW. Если ETW недоступен или logger занят, PortSentinel безопасно переключается на Windows IP Helper API snapshot fallback. Результат автоматически архивируется.

## Archive Search

Поиск работает по локальной SQLite-базе и использует параметризованные queries. Доступны:

- свободный поиск по process name, local/remote IP и diagnostic note;
- события `RETRANSMIT`;
- события `DISCONNECT`;
- все события из `SnapshotFallback` captures;
- fallback listeners.

## Selective Comparison

Пользователь выбирает две capture-сессии из последних 50 записей. PortSentinel автоматически определяет более старую и новую запись и сравнивает их по lifecycle fingerprint.

Fingerprint включает event kind, protocol, endpoints и process name, но не PID. Diff является диагностическим metadata и не формирует threat verdict.

## Retention Center

Retention Center показывает:

- количество captures и events;
- oldest/newest capture;
- текущий размер SQLite-файла;
- число captures/events, которые будут удалены.

Сначала всегда отображается **dry-run preview**. Очистка выполняется только после подтверждения клавишей `Y`. Удаляются только старые `telemetry_captures` и связанные `telemetry_events`; sessions, baselines и reports не затрагиваются.

## Privacy boundary

PortSentinel не собирает и не сохраняет packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое. ETW events, endpoints, DNS names и lifecycle diff являются диагностическими metadata, а не malware verdict.

## Быстрый старт ⚡

1. Откройте раздел **Releases**.
2. Скачайте `PortSentinel-0.5.3-win-x64.zip`.
3. Сверьте архив с `PortSentinel-0.5.3-win-x64.zip.sha256`.
4. Полностью распакуйте ZIP в отдельную папку.
5. Запустите `portsentinel.exe`.

Не запускайте программу непосредственно из ZIP: встроенному обновлению нужна обычная папка с правом записи.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта, capture или события |
| `W` / `S` | Альтернативная навигация в главном меню |
| `Enter` | Открыть экран или подтвердить выбор |
| `J` / `M` | Экспорт JSON / Markdown |
| `X` | Показать missing fingerprints |
| `Y` | Подтвердить retention после preview |
| `Esc` / `Q` | Назад или выход |

## Локальное хранилище

```text
%LocalAppData%\PortSentinel\portsentinel.db
%LocalAppData%\PortSentinel\reports
```

SQLite использует WAL. Telemetry archive хранится в таблицах `telemetry_captures` и `telemetry_events` без изменения существующих sessions и baselines.

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

Portable EXE:

```powershell
dotnet publish src/PortSentinel/PortSentinel.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## Roadmap 🗺️

- `0.5.x` — IPv6/UDP ETW coverage, failure classification и automated tests;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

## Автор

**Onmaynec** — [@Onmaynec](https://github.com/Onmaynec)

---

**PortSentinel 0.5.3** · Windows 10/11 x64 · .NET 8 · MIT
