<div align="center">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.1-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для сетевых snapshots, ETW-событий, истории и объяснимого анализа.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`, а не оболочка над `netstat` или набор PowerShell-команд.

## Что нового в 0.5.1 ⚡

- добавлена верхнеуровневая панель **ETW Telemetry**;
- read-only kernel ETW backend получает TCP IPv4 connect, accept, disconnect и retransmit events;
- capture выполняется в ограниченном 12-секундном окне;
- доступна подробная карточка event metadata;
- capability probe показывает наличие elevated access;
- при недостаточных правах или ошибке ETW автоматически включается snapshot fallback;
- ETW capture экспортируется в JSON и Markdown;
- все возможности Extended Telemetry v0.5.0 сохранены во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| ⚡ ETW Network Capture | Получает kernel TCP lifecycle events без packet payload |
| 🛟 Snapshot Fallback | Продолжает работу через Windows IP Helper API без ETW |
| 👁️ Application Watch | Строит timeline процесса и ищет reconnect loops |
| 🌐 DNS Correlation | Выполняет ограниченный reverse DNS с timeout и кэшем |
| 🌳 Network Process Tree | Показывает родительские процессы через Toolhelp32 |
| ⇄ Session Comparison | Сравнивает сохранённые SQLite-сессии |
| 🎯 Baseline & Rules | Показывает deviations, evidence и limitations |
| 🔐 Executable Enrichment | Рассчитывает SHA-256 и читает Authenticode metadata |
| 🔄 Update Center | Проверяет GitHub Releases, ZIP и SHA-256 |

## ETW Telemetry

1. Откройте **ETW Network Capture**.
2. PortSentinel проверит возможность управления kernel ETW session.
3. При наличии прав будет выполнен 12-секундный capture.
4. Без elevated access или при ошибке программа покажет обычный snapshot вместо аварийного завершения.
5. Нажмите `J` или `M`, чтобы экспортировать результат.

Kernel ETW control обычно требует запуска от администратора. PortSentinel не изменяет системные настройки и не добавляет пользователя в группы доступа.

## Privacy boundary

PortSentinel не собирает packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое. ETW events, DNS names и reconnect indicators являются диагностическими metadata, а не malware verdict.

## Быстрый старт ⚡

1. Откройте раздел **Releases**.
2. Скачайте `PortSentinel-0.5.1-win-x64.zip`.
3. Сверьте архив с `PortSentinel-0.5.1-win-x64.zip.sha256`.
4. Полностью распакуйте ZIP в отдельную папку.
5. Запустите `portsentinel.exe`.

Не запускайте программу непосредственно из ZIP: встроенному обновлению нужна обычная папка с правом записи.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта или события |
| `W` / `S` | Альтернативная навигация в главном меню |
| `Enter` | Открыть экран или карточку события |
| `R` | Повторить capture или анализ |
| `J` / `M` | Экспорт JSON / Markdown |
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

Portable EXE:

```powershell
dotnet publish src/PortSentinel/PortSentinel.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true
```

## Roadmap 🗺️

- `0.5.x` — IPv6/UDP ETW coverage, timeline persistence и tests;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

## Автор

**Onmaynec** — [@Onmaynec](https://github.com/Onmaynec)

---

**PortSentinel 0.5.1** · Windows 10/11 x64 · .NET 8 · MIT
