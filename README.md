<div align="center">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.5.8-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная Windows TUI для ETW telemetry, безопасного session control, SQLite archive и explainable network diagnostics.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельный `portsentinel.exe`. Он не перехватывает packet payload, не расшифровывает TLS и не является malware scanner.

## Что нового в 0.5.8 🛡️

- новая панель **ETW Session Guard**;
- preflight inventory активных ETW session names;
- разделение собственных `PortSentinel-*` и foreign sessions;
- 15-секундный Guarded Capture с сохранением в SQLite;
- diagnostics попыток запуска, backend и snapshot fallback;
- best-effort classification access denied, name collision, resource limit и unavailable session;
- один bounded retry только при вероятном name collision;
- JSON/Markdown exports inventory и capture diagnostics;
- dry-run cleanup orphan sessions с обязательным подтверждением `Y`;
- cleanup разрешён только для session names с префиксом `PortSentinel-`;
- Installer Watch v0.5.7 полностью сохранён во вложенной панели.

## Основные возможности ✨

| Модуль | Что делает |
|---|---|
| 🛡️ ETW Session Guard | Проверяет logger sessions и защищает foreign sessions от изменений |
| 📦 Installer Watch | Сравнивает baseline и watch во время ручной установки программы |
| 📜 Timeline Explorer | Постранично открывает крупные captures через SQLite |
| 🌐 Network Coverage | Показывает TCP4/TCP6/UDP4/UDP6 protocol families |
| ❤️‍🩹 Connection Health | Анализирует fail/reconnect/retransmit patterns |
| ⚡ ETW Network Capture | Получает read-only kernel network metadata |
| 🛟 Snapshot Fallback | Продолжает работу через Windows IP Helper API |
| 🗄️ Telemetry Archive | Хранит captures и events в локальной SQLite-базе |
| 🔎 Archive Search | Ищет события по process, IP, note, kind и backend |
| ⇄ Selective Comparison | Сравнивает выбранные capture-сессии без PID |
| 🧹 Retention Center | Выполняет preview и безопасную очистку старого archive |
| 🎯 Baseline & Rules | Показывает deviations, evidence и limitations |
| 🔄 Update Center | Проверяет GitHub Releases и SHA-256 |

## ETW Session Guard

### Guarded Capture

1. Session Guard получает список активных ETW session names.
2. PortSentinel выполняет bounded 15-секундный capture.
3. При вероятном name collision допускается один короткий retry.
4. При отказе используется snapshot fallback через Windows IP Helper API.
5. Capture сохраняется в SQLite, а diagnostics можно экспортировать в JSON или Markdown.

Foreign sessions только отображаются. PortSentinel не останавливает, не перезапускает и не изменяет их.

### Session Inventory

Inventory показывает:

- количество активных sessions;
- session names;
- собственные sessions с префиксом `PortSentinel-`;
- foreign sessions;
- ошибку inventory, если Windows не разрешила query.

Inventory не показывает packet data и не изменяет logger configuration.

### Owned Cleanup

Cleanup предназначен для orphan sessions после аварийного завершения приложения.

- сначала показывается dry-run список;
- foreign sessions исключаются по ownership filter;
- выполнение начинается только после нажатия `Y`;
- перед cleanup необходимо закрыть другие экземпляры PortSentinel.

Другой активный экземпляр PortSentinel также использует префикс `PortSentinel-`, поэтому его capture может быть остановлен подтверждённым cleanup.

## Installer Watch

Installer Watch остаётся доступным во вложенной панели:

- Standard Watch: baseline 8 секунд + watch 30 секунд;
- Deep Watch: baseline 10 секунд + watch 60 секунд;
- ручная точка запуска установщика;
- PID-независимый before/after diff;
- process candidates, endpoints, TCP/UDP counts и failure signals;
- JSON/Markdown reports.

PortSentinel не запускает installer EXE самостоятельно и не доказывает attribution endpoint конкретному приложению.

## ETW coverage

Kernel backend обрабатывает:

- TCP4: connect, accept, disconnect, retransmit, reconnect и fail;
- TCP6: connect, accept, disconnect, retransmit и reconnect;
- UDP4: send и receive;
- UDP6: send и receive.

Kernel ETW control обычно требует запуска от администратора. При недостаточных правах, logger/resource conflict или другой ошибке используется snapshot fallback.

## Privacy boundary

PortSentinel не собирает и не сохраняет packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое.

## Быстрый старт ⚡

1. Скачайте `PortSentinel-0.5.8-win-x64.zip` из Releases.
2. Сверьте архив с `.sha256`.
3. Полностью распакуйте ZIP в отдельную папку.
4. Для kernel ETW запустите `portsentinel.exe` от администратора.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта меню |
| `Enter` | Открыть действие или начать capture |
| `R` | Обновить session inventory |
| `Y` | Подтвердить owned-session cleanup |
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

## Roadmap 🗺️

- `0.5.x` — automated tests и documented failure-code mapping;
- `0.6.0` — read-only Firewall correlation и безопасные managed rules;
- `1.0.0` — стабильные schemas, подписанные релизы и backward compatibility.

---

**PortSentinel 0.5.8** · Windows 10/11 x64 · .NET 8 · MIT
