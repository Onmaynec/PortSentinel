# PortSentinel 0.5.8 — ETW Session Guard

Версия 0.5.8 добавляет безопасный контроль состояния ETW logger sessions перед capture. PortSentinel показывает активные session names, отделяет собственные сессии от чужих, сохраняет диагностику запуска и корректно использует snapshot fallback, не вмешиваясь в работу сторонних инструментов.

## Главное

- новая верхнеуровневая панель **ETW Session Guard**;
- preflight inventory активных ETW session names;
- разделение `PortSentinel-*` и foreign sessions;
- 15-секундный Guarded Capture с автоматическим сохранением в SQLite;
- диагностика попыток запуска, backend, fallback и количества активных sessions;
- best-effort классификация access denied, name collision, resource limit и unavailable session;
- один bounded retry только при вероятном name collision;
- JSON schema v1 и Markdown exports inventory/diagnostics;
- dry-run cleanup orphan sessions с подтверждением клавишей `Y`;
- cleanup допускается только для имён с префиксом `PortSentinel-`;
- Installer Watch v0.5.7 полностью сохранён во вложенной панели.

## Safety boundary

PortSentinel никогда автоматически не останавливает, не перезапускает и не изменяет foreign ETW sessions. Cleanup требует явного подтверждения и применяет ownership filter до attach/stop. Перед cleanup следует закрыть другие экземпляры PortSentinel, потому что активная сессия другого экземпляра также использует префикс `PortSentinel-`.

## Fallback и ограничения

Kernel ETW control обычно требует elevated access. При недостаточных правах, logger/resource conflict или другой ошибке сохраняется snapshot fallback через Windows IP Helper API. Классификация текстовых ошибок является диагностической и не заменяет Windows Event Log или vendor-specific troubleshooting.

Inventory содержит только имена ETW sessions. Capture хранит только network metadata. Packet payload, HTTP body, cookies, credentials, tokens и расшифрованное TLS-содержимое не собираются.

## Скачать

Используйте `PortSentinel-0.5.8-win-x64.zip`, проверьте файл `.sha256` и полностью распакуйте архив перед запуском.
