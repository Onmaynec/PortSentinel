<div align="center">

<img src="assets/logo.svg" width="820" alt="PortSentinel">

# PortSentinel 🛡️

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows)](docs/COMPATIBILITY.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](src/PortSentinel/PortSentinel.csproj)
[![Version](https://img.shields.io/badge/version-0.2.0-00d4ff)](VERSION)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Полноэкранная консольная утилита для контроля сетевой активности Windows.**

[English](docs/README_EN.md) · [Интерфейс](docs/INTERFACE.md) · [Архитектура](docs/ARCHITECTURE.md) · [Обновления](docs/UPDATES.md) · [Roadmap](docs/ROADMAP.md)

</div>

> [!IMPORTANT]
> PortSentinel — самостоятельная Windows-программа `portsentinel.exe`. Она не является расширением CMD, набором команд PowerShell или оболочкой над `netstat`.

## Что изменилось в 0.2.0 🚀

- добавлена полноценная полноэкранная **TUI-панель** в стиле Network Control Center;
- меню управляется стрелками `↑` / `↓`, клавишей `Enter` и быстрыми цифровыми клавишами;
- добавлены ASCII-логотип, цветовые статусы, экранные переходы, spinner и progress-анимации;
- реализован живой TCP/UDP-монитор с обновлением каждую секунду;
- соединения и listeners связываются с PID и именем процесса через Windows IP Helper API;
- поддерживаются IPv4 и IPv6 таблицы;
- добавлены отдельные экраны listeners, активных подключений, карточки процесса и Quick Scan;
- реализована проверка обновлений через GitHub Releases;
- обновление скачивает ZIP, проверяет SHA-256, заменяет файлы и перезапускает программу;
- GitHub Actions автоматически собирает portable ZIP и публикует Release при изменении `VERSION`;
- русский язык и русская документация установлены по умолчанию.

## Как выглядит панель 💻

```text
╔═╗╔═╗╦═╗╔╦╗  ╔═╗╔═╗╔╗╔╔╦╗╦╔╗╔╔═╗╦
╠═╝║ ║╠╦╝ ║   ╚═╗║╣ ║║║ ║ ║║║║║╣ ║
╩  ╚═╝╩╚═ ╩   ╚═╝╚═╝╝╚╝ ╩ ╩╝╚╝╚═╝╩

  CONTROL CENTER  v0.2.0  •  WINDOWS NETWORK CONTROL CENTER
────────────────────────────────────────────────────────────────────────

  ▶ [1] LIVE NETWORK MONITOR   TCP/UDP в реальном времени
    [2] LISTENING PORTS        TCP listeners и UDP endpoints
    [3] ACTIVE CONNECTIONS     Внешние и локальные соединения
    [4] PROCESS INSPECTOR      Карточки сетевых процессов
    [5] QUICK SCAN             Поиск необычных listeners и путей
    [6] UPDATE CENTER          GitHub Releases и автоустановка
    [7] ABOUT / SYSTEM         Версия и ограничения
    [0] EXIT                   Завершить PortSentinel
```

## Возможности ✨

| Модуль | Что делает |
|---|---|
| 📡 Live Monitor | Обновляет TCP/UDP таблицу в реальном времени |
| 🔌 Listening Ports | Показывает TCP listeners и UDP endpoints |
| 🌐 Connections | Показывает локальные и внешние подключения |
| 🧩 Process Mapping | Сопоставляет запись с PID, процессом и executable path |
| 🕵️ Quick Scan | Выделяет wildcard listeners, Temp/Downloads и ограниченные metadata |
| 🧾 Process Inspector | Показывает процесс, endpoint, компанию, путь и время запуска |
| 🎞️ Animations | Spinner, progress bar, заставка и цветовые статусы |
| 🔄 Auto Update | Проверяет GitHub Release, SHA-256 и устанавливает ZIP |
| 📦 Portable Release | Один self-contained `portsentinel.exe`, .NET отдельно не нужен |

## Быстрый старт ⚡

1. Откройте раздел **Releases**.
2. Скачайте `PortSentinel-0.2.0-win-x64.zip`.
3. Сверьте архив с `PortSentinel-0.2.0-win-x64.zip.sha256`.
4. Полностью распакуйте ZIP в отдельную папку.
5. Запустите `portsentinel.exe`.

Не запускайте программу непосредственно из ZIP — встроенному обновлению нужна обычная папка с правом записи.

## Управление 🎮

| Клавиша | Действие |
|---|---|
| `↑` / `↓` | Выбор пункта меню или процесса |
| `W` / `S` | Альтернативная навигация по главному меню |
| `Enter` | Открыть экран или подробную карточку |
| `0`–`7` | Быстрый переход из главного меню |
| `R` | Принудительно обновить сетевой снимок |
| `Esc` / `Q` | Назад или выход |

## Командные параметры

Основной режим — интерактивная панель. Небольшой набор параметров оставлен для диагностики и автоматизации:

```powershell
portsentinel.exe --version
portsentinel.exe --check-update
portsentinel.exe --no-animation
portsentinel.exe --help
```

## Как собирается Release 📦

Версия хранится в файле [`VERSION`](VERSION). После изменения версии и загрузки в `main` workflow:

1. собирает self-contained single-file EXE для `win-x64`;
2. добавляет README, CHANGELOG и LICENSE;
3. создаёт ZIP;
4. рассчитывает SHA-256;
5. создаёт тег `vX.Y.Z`;
6. публикует или обновляет GitHub Release.

Подробности: [`docs/UPDATES.md`](docs/UPDATES.md).

## Безопасность и ограничения 🔒

- PortSentinel не собирает payload, HTTP body, cookies, токены или содержимое TLS;
- Quick Scan показывает признаки, требующие проверки, но не объявляет процесс вредоносным;
- часть metadata системных и protected-процессов может быть недоступна без администратора;
- UDP endpoint не содержит выдуманный remote address;
- встроенное обновление принимает пакеты только из Releases этого репозитория и проверяет SHA-256;
- Firewall, baseline и история сессий запланированы для следующих версий.

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

- `0.2.x` — стабилизация TUI, сортировка и дополнительные фильтры;
- `0.3.0` — SQLite-сессии, история и экспорт отчётов;
- `0.4.0` — baseline и explainable rule engine;
- `0.5.0` — безопасная интеграция с Windows Firewall;
- `1.0.0` — стабильный формат данных, подписанные релизы и полный набор проверок.

## Автор

**Onmaynec** — [@Onmaynec](https://github.com/Onmaynec)

---

**PortSentinel 0.2.0** · Windows 10/11 x64 · .NET 8 · MIT
