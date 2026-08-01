# PortSentinel 0.4.0 — Explainable Rules

Версия 0.4.0 превращает baseline deviations в объяснимый rule engine с локальным enrichment executable.

## Главное

- новый экран **Explainable Rules**;
- стабильные baseline fingerprints без зависимости от PID;
- правило нового listener относительно профиля `default`;
- правило wildcard listener;
- правило сетевого executable без Authenticode;
- правило активности из Temp или Downloads;
- severity, confidence, evidence и limitation для каждого finding;
- SHA-256 и publisher в карточке executable;
- сохранены Session History, exports, Network Tools и updater.

## Модель доверия

PortSentinel показывает наблюдаемые факты и ограничения анализа. Finding не является malware verdict. Отсутствие подписи, wildcard binding или новый listener могут иметь легитимную причину.

## Скачать

Используйте `PortSentinel-0.4.0-win-x64.zip`. Файл `.sha256` содержит контрольную сумму архива.

Полностью распакуйте архив перед запуском.
