# PortSentinel 0.5.2 — Telemetry Archive

Версия 0.5.2 превращает одноразовые ETW/fallback captures в локальный архив с историей, просмотром событий и сравнением сетевого lifecycle между запусками.

## Главное

- новая панель **Telemetry Archive**;
- автоматическое сохранение каждого ETW или snapshot fallback capture в SQLite;
- история до 100 последних capture-сессий;
- просмотр сохранённых event metadata и backend status;
- JSON schema v1 и Markdown export архивных capture;
- сравнение двух последних capture по lifecycle fingerprint без PID;
- список новых и исчезнувших lifecycle fingerprints;
- JSON/Markdown export telemetry diff;
- полный ETW Control Center v0.5.1 сохранён внутри новой панели.

## Хранилище и совместимость

В существующую базу `%LocalAppData%\PortSentinel\portsentinel.db` добавляются таблицы `telemetry_captures` и `telemetry_events`. Они создаются через `CREATE TABLE IF NOT EXISTS`; существующие sessions, baselines и reports не изменяются.

## Privacy boundary

Архив хранит только нормализованные timestamps, event kinds, process metadata и endpoints. Packet payload, HTTP body, cookies, credentials, tokens и расшифрованное TLS-содержимое не сохраняются.

Lifecycle fingerprint исключает PID и используется только для диагностического сравнения. Diff не является malware или threat verdict.

## Скачать

Используйте `PortSentinel-0.5.2-win-x64.zip`. Файл `.sha256` содержит контрольную сумму архива.

Полностью распакуйте архив перед запуском.
