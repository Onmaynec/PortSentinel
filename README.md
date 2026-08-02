<div align="center">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.0-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для наблюдения, истории и объяснимого анализа сетевой активности.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`. Он не является расширением CMD, набором PowerShell-команд или оболочкой над `netstat`.

## Что нового в 0.5.0 📡

- добавлена верхнеуровневая панель **Extended Telemetry**;
- Application Watch наблюдает выбранный процесс и строит timeline endpoint;
- фиксируются first seen, last seen, observations и connection cycles;
- повторные появления одинакового remote endpoint выделяются как reconnect loops;
- после завершения watch автоматически создаются JSON и Markdown отчёты;
- добавлена best-effort reverse DNS correlation с timeout и кэшем;
- Network Process Tree показывает сетевые процессы и их родителей через Windows Toolhelp32;
- Session Comparison сравнивает две последние SQLite-сессии по стабильному fingerprint;
- полный Control Center v0.4.0 с sessions, baseline и explainable rules сохранён внутри новой панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| 👁️ Application Watch | Строит timeline выбранного процесса и ищет reconnect loops |
| 🌐 DNS Correlation | Выполняет ограниченный reverse DNS для внешних IP |
| 🌳 Network Process Tree | Показывает родительские процессы сетевой активности |
| ⇄ Session Comparison | Сравнивает две последние сохранённые сессии |
| 📡 Live Session Recorder | Показывает TCP/UDP и сохраняет уникальные записи в SQLite |
| 🎯 Baseline Center | Создаёт профиль `default` и показывает отклонения |
| 🧠 Explainable Rules | Показывает severity, confidence, evidence и limitations |
| 🔐 Executable Enrichment | Рассчитывает SHA-256 и читает Authenticode metadata |
| 🔄 Update Center | Проверяет GitHub Releases, ZIP и SHA-256 |

## Application Watch

1. Выберите процесс с текущей сетевой активностью.
2. PortSentinel раз в секунду читает Windows TCP/UDP tables.
3. Для каждого endpoint сохраняются first/last seen, число samples и connect cycles.
4. Три и более появления одного process/remote endpoint считаются reconnect loop.
5. При выходе выполняется DNS enrichment и сохраняются JSON/Markdown reports.

Application Watch не перехватывает payload, HTTP body, cookies, токены или содержимое TLS.

## Быстрый старт ⚡

1. Откройте раздел **Releases**.
2. Скачайте `PortSentinel-0.5.0-win-x64.zip`.
3. Сверьте архив с `PortSentinel-0.5.0-win-x64.zip.sha256`.
4. Полностью распакуйте ZIP в отдельную папку.
5. Запустите `portsentinel.exe`.

Не запускайте программу непосредственно из ZIP: встроенному обновлению нужна обычная папка с правом записи.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта, процесса или finding |
| `W` / `S` | Альтернативная навигация в главном меню |
| `Enter` | Открыть экран или начать watch |
| `R` | Повторить снимок или анализ |
| `C` | Создать baseline |
| `J` / `M` | Экспорт JSON / Markdown |
| `Esc` / `Q` | Назад, завершить watch или выйти |

## Хранилище

```text
%LocalAppData%\PortSentinel\portsentinel.db
%LocalAppData%\PortSentinel\reports
```

SQLite использует WAL mode. Timeline и session diff сохраняются локально.

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

- `0.5.x` — ETW backend, timeline persistence и расширенная DNS correlation;
- `0.6.0` — безопасная интеграция с Windows Firewall;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

## Автор

**Onmaynec** — [@Onmaynec](https://github.com/Onmaynec)

---

**PortSentinel 0.5.0** · Windows 10/11 x64 · .NET 8 · MIT
