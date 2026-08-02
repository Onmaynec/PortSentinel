# PortSentinel 0.5.7 — Installer Watch

Версия 0.5.7 добавляет управляемый before/after workflow для наблюдения за сетевой metadata во время установки программ. PortSentinel записывает baseline, ждёт ручного запуска установщика, выполняет watch capture и строит объяснимый diff.

## Главное

- новая панель **Installer Watch**;
- Standard Watch: baseline 8 секунд и watch 30 секунд;
- Deep Watch: baseline 10 секунд и watch 60 секунд;
- обе capture-сессии автоматически сохраняются в SQLite;
- optional process hint приоритизирует похожие process names;
- добавленные events группируются по процессу, endpoints и TCP/UDP family;
- отдельно считаются `FAIL`, `RETRANSMIT` и `RECONNECT` signals;
- доступен анализ последней пары архивных captures;
- added metadata, process candidates и limitations доступны в TUI;
- JSON schema v1 и Markdown reports;
- Timeline Explorer v0.5.6 полностью сохранён внутри новой панели.

## Как работает

1. Закройте лишние приложения и запишите baseline.
2. Запустите установщик вручную.
3. Вернитесь в PortSentinel и начните watch capture.
4. После capture программа сравнит PID-независимые network fingerprints.
5. Process hint будет использован только для сортировки кандидатов.

PortSentinel не запускает installer EXE и не изменяет систему.

## Модель доверия

Installer Watch не доказывает, что конкретное приложение владеет endpoint. Новые события могут принадлежать фоновым приложениям, службам или scheduled tasks. Установщик может делегировать трафик child processes, service hosts, package managers или browser.

Baseline и watch являются отдельными bounded captures. События между ними не записываются. Outbound ephemeral local port исключается из fingerprint для снижения ожидаемого шума.

## Privacy boundary

Сохраняется только network metadata. Packet payload, HTTP body, cookies, credentials, tokens и расшифрованное TLS-содержимое не собираются.

## Скачать

Используйте `PortSentinel-0.5.7-win-x64.zip`, проверьте файл `.sha256` и полностью распакуйте архив перед запуском.
