# PortSentinel 0.5.1 — ETW Telemetry

Версия 0.5.1 добавляет дополнительный read-only kernel ETW backend для событий жизненного цикла TCP-соединений и сохраняет безопасный snapshot fallback.

## Главное

- новая панель **ETW Telemetry**;
- kernel TCP IPv4 events: connect, accept, disconnect и retransmit;
- фиксированное 12-секундное окно capture;
- список событий и подробная карточка process/endpoints;
- capability probe для elevated access;
- автоматический fallback на Windows IP Helper API snapshot;
- JSON schema v1 и Markdown export;
- полный Extended Telemetry Control Center v0.5.0 сохранён внутри новой панели.

## Доступ и fallback

Управление kernel ETW session обычно требует запуска от администратора. Если права отсутствуют, системная ETW session занята или backend возвращает ошибку, PortSentinel не завершается аварийно и показывает обычный snapshot текущих TCP/UDP таблиц.

## Privacy boundary

PortSentinel собирает только event metadata и endpoints. Packet payload, HTTP body, cookies, credentials, tokens и расшифрованное TLS-содержимое не собираются.

## Скачать

Используйте `PortSentinel-0.5.1-win-x64.zip`. Файл `.sha256` содержит контрольную сумму архива.

Полностью распакуйте архив перед запуском.
