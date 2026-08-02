# PortSentinel 0.5.4 — Connection Health

Версия 0.5.4 расширяет kernel ETW capture событиями fail/reconnect и добавляет объяснимую диагностику качества соединений для live и архивных captures.

## Главное

- новая панель **Connection Health**;
- kernel TCP `FAIL` и `RECONNECT` events через TraceEvent;
- сохранение protocol и numeric failure code как evidence;
- 15-секундный Capture & Health с автоматическим архивированием;
- анализ последней или выбранной capture-сессии;
- findings для kernel failures, retransmit bursts, reconnect loops и rapid repeated connects;
- capture-boundary indicator для disconnect без connect внутри окна;
- health score 0–100: Stable, Observe, Degraded или Critical;
- подробные evidence, confidence и limitations;
- JSON и Markdown health reports;
- полный Archive Operations v0.5.3 сохранён внутри новой панели.

## Модель доверия

PortSentinel не расшифровывает numeric kernel failure codes предположениями: исходный code и protocol сохраняются в evidence. Retransmits, reconnects и repeated connects могут иметь штатные причины, поэтому каждый finding содержит limitation.

Health score является удобным diagnostic summary и не считается malware, ownership или security verdict.

## Privacy boundary

Packet payload, HTTP body, cookies, credentials, tokens и расшифрованное TLS-содержимое не собираются и не сохраняются.

## Скачать

Используйте `PortSentinel-0.5.4-win-x64.zip` и проверьте файл `.sha256`. Полностью распакуйте архив перед запуском.
