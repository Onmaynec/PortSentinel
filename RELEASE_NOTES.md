# PortSentinel 0.5.5 — Network Coverage

Версия 0.5.5 расширяет read-only kernel ETW capture до TCP/UDP для IPv4 и IPv6 и добавляет отдельные coverage reports для live и архивных captures.

## Главное

- новая панель **Network Coverage**;
- TCP IPv6 connect, accept, disconnect, retransmit и reconnect events;
- UDP IPv4/IPv6 send и receive events;
- единые protocol labels `TCP4`, `TCP6`, `UDP4`, `UDP6`;
- 15-секундный Coverage Capture с автоматическим архивированием;
- анализ последней или выбранной archive capture;
- protocol matrix: events, processes, remote endpoints и directions;
- IPv4/IPv6 и TCP/UDP distribution;
- top remote endpoints;
- JSON schema v1 и Markdown coverage reports;
- обычный ETW export обновлён до schema v3;
- полный Connection Health v0.5.4 сохранён внутри новой панели.

## Исправление портов

TraceEvent уже возвращает TCP/UDP port values в host byte order. Версия 0.5.5 удаляет повторный byte-swap, который мог отображать ETW-порты неверно.

## Ограничения UDP и coverage

Некоторые kernel UDP callbacks не предоставляют source port. В таком случае PortSentinel сохраняет `0` и явно показывает limitation. Coverage описывает только события внутри выбранного окна: отсутствие family в отчёте не доказывает отсутствие трафика.

## Privacy boundary

PortSentinel собирает только kernel event metadata. Packet payload, HTTP body, cookies, credentials, tokens и расшифрованное TLS-содержимое не собираются и не сохраняются.

## Скачать

Используйте `PortSentinel-0.5.5-win-x64.zip` и проверьте файл `.sha256`. Полностью распакуйте архив перед запуском.
