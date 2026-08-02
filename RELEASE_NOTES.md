# PortSentinel 0.5.0 — Extended Telemetry

Версия 0.5.0 добавляет наблюдение за жизненным циклом сетевой активности без перехвата payload и без обязательных прав администратора.

## Главное

- новая панель **Extended Telemetry**;
- Application Watch для выбранного сетевого процесса;
- first seen, last seen, observations и connection cycles;
- обнаружение повторяющихся reconnect loops;
- автоматический JSON/Markdown timeline report;
- reverse DNS correlation для внешних IP с timeout и кэшем;
- Network Process Tree через Windows Toolhelp32;
- сравнение двух последних SQLite-сессий;
- diff endpoints, listeners, внешних соединений и процессов;
- JSON/Markdown export session diff;
- полный Control Center v0.4.0 сохранён внутри новой панели.

## Модель данных

Application Watch использует периодические снимки Windows TCP/UDP tables. PortSentinel не сохраняет payload, HTTP body, cookies, токены или расшифрованное TLS-содержимое.

DNS и reconnect findings являются диагностическими metadata, а не malware verdict. ETW backend запланирован для ветки 0.5.x после стабилизации snapshot timeline.

## Скачать

Используйте `PortSentinel-0.5.0-win-x64.zip`. Файл `.sha256` содержит контрольную сумму архива.

Полностью распакуйте архив перед запуском.
