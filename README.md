<div align="center">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.4.0-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для наблюдения, истории и объяснимого анализа сетевой активности.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`. Он не является расширением CMD, набором PowerShell-команд или оболочкой над `netstat`.

## Что нового в 0.4.0 🔎

- добавлен **Explainable Rules Center**;
- baseline сравнивается по стабильным fingerprints, не зависящим от PID;
- реализованы `NewListenerRule`, `WildcardListenerRule`, `UnsignedNetworkProcessRule` и `TempDirectoryNetworkProcessRule`;
- каждый finding содержит severity, confidence, evidence и limitation;
- executable обогащаются SHA-256;
- определяется наличие Authenticode-подписи и издатель сертификата;
- findings остаются объяснимыми сигналами, а не malware verdict;
- сохранены SQLite Session History, JSON/Markdown exports и все сетевые инструменты предыдущих версий.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| 📡 Live Session Recorder | Показывает TCP/UDP и сохраняет уникальные записи в SQLite |
| 🗂️ Session History | Открывает сохранённые сессии и экспортирует JSON/Markdown |
| 🎯 Baseline Center | Создаёт профиль `default` и показывает отклонения |
| 🧠 Explainable Rules | Применяет правила и показывает evidence/confidence/limitations |
| 🔐 Executable Enrichment | Рассчитывает SHA-256 и читает Authenticode metadata |
| 🔌 Network Tools | Live Monitor, listeners, connections, process cards и Quick Scan |
| 🔄 Update Center | Проверяет GitHub Releases, ZIP и SHA-256 |
| 📦 Portable Release | Self-contained Windows x64 executable |

## Правила v0.4.0

| Rule | Сигнал |
|---|---|
| `PS-RULE-001` | Новый listener относительно baseline |
| `PS-RULE-002` | Listener на wildcard-адресе |
| `PS-RULE-003` | Сетевой executable без Authenticode-подписи |
| `PS-RULE-004` | Сетевая активность из Temp или Downloads |

PortSentinel не объявляет процесс вредоносным. Любой finding необходимо проверять в контексте установленного ПО, Firewall, маршрутизации и ожидаемой конфигурации системы.

## Быстрый старт ⚡

1. Откройте раздел **Releases**.
2. Скачайте `PortSentinel-0.4.0-win-x64.zip`.
3. Сверьте архив с `PortSentinel-0.4.0-win-x64.zip.sha256`.
4. Полностью распакуйте ZIP в отдельную папку.
5. Запустите `portsentinel.exe`.

Не запускайте программу непосредственно из ZIP: встроенному обновлению нужна обычная папка с правом записи.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта или finding |
| `W` / `S` | Альтернативная навигация в главном меню |
| `Enter` | Открыть экран или evidence |
| `R` | Повторить снимок или анализ |
| `C` | Создать baseline |
| `J` / `M` | Экспорт JSON / Markdown |
| `Esc` / `Q` | Назад или выход |

## Хранилище

```text
%LocalAppData%\PortSentinel\portsentinel.db
%LocalAppData%\PortSentinel\reports
```

SQLite использует WAL mode. PortSentinel не сохраняет payload, HTTP body, cookies, токены или расшифрованное TLS-содержимое.

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

- `0.4.x` — тесты rule engine, фильтры findings и экспорт rule report;
- `0.5.0` — ETW, DNS correlation, process tree и timeline;
- `0.6.0` — безопасная интеграция с Windows Firewall;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

## Автор

**Onmaynec** — [@Onmaynec](https://github.com/Onmaynec)

---

**PortSentinel 0.4.0** · Windows 10/11 x64 · .NET 8 · MIT
