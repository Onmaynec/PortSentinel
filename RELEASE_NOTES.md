# PortSentinel 0.5.3 — Archive Operations

Версия 0.5.3 добавляет управляемые capture profiles и полноценные операции над локальным telemetry archive: поиск, выбор произвольной пары для comparison и безопасную retention-очистку.

## Главное

- новая панель **Archive Operations**;
- capture profiles на 5, 15, 30 и 60 секунд;
- автоматическое архивирование результата каждого profile capture;
- поиск по process name, local/remote IP и diagnostic notes;
- preset-фильтры для retransmit, disconnect, fallback и listener events;
- selective comparison любой пары из последних 50 captures;
- Archive Status с количеством captures/events, диапазоном дат и размером базы;
- retention policies: сохранить последние 25, 50, 100 или 250 captures;
- обязательный dry-run preview до удаления;
- удаление только после явного подтверждения клавишей `Y`;
- полный Telemetry Archive v0.5.2 сохранён внутри новой панели.

## Retention safety

Retention удаляет только старые строки `telemetry_captures`. Связанные `telemetry_events` очищаются каскадно внутри транзакции. Таблицы sessions, baselines и сохранённые reports не изменяются.

## Search and comparison

Поиск использует параметризованные SQLite queries. Selective comparison применяет тот же lifecycle fingerprint без PID, который появился в v0.5.2. Результат является диагностическим diff и не формирует malware или threat verdict.

## Privacy boundary

PortSentinel по-прежнему не собирает packet payload, HTTP body, cookies, credentials, tokens или расшифрованное TLS-содержимое.

## Скачать

Используйте `PortSentinel-0.5.3-win-x64.zip`. Файл `.sha256` содержит контрольную сумму архива.

Полностью распакуйте архив перед запуском.
