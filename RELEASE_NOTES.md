# PortSentinel 0.5.6 — Timeline Explorer

Версия 0.5.6 делает telemetry archive удобным для больших captures: capture index и event timeline теперь читаются отдельными SQL-pages, поддерживают фильтры и не требуют загружать все события в память.

## Главное

- новая панель **Timeline Explorer**;
- server-side pagination capture index и event timeline;
- PageUp/PageDown для перехода между страницами;
- Home/End для первой и последней страницы;
- kind presets: connect, accept, disconnect, retransmit, reconnect, fail, UDP send/receive, listener и snapshot;
- protocol presets: `TCP4`, `TCP6`, `UDP4`, `UDP6`;
- text search по process name, IP address, port и diagnostic note;
- переход к точному sequence number с вычислением нужной страницы;
- JSON/Markdown export текущей отфильтрованной SQL-page;
- полный Network Coverage v0.5.5 сохранён внутри новой панели.

## Масштабирование

`TimelineExplorerService` выполняет отдельный `COUNT(*)` и paged query с `LIMIT/OFFSET`. Размер страницы подстраивается под высоту терминала. Крупный capture не материализуется целиком при просмотре, фильтрации или переходе к sequence.

Для ускорения создаются backward-compatible indexes:

- `capture_id + sequence`;
- `capture_id + kind + sequence`;
- `capture_id + protocol + sequence`.

## Безопасность поиска

Свободный текст передаётся SQLite через parameter. Символы `%`, `_` и `\\` экранируются и рассматриваются как обычный текст. Таблицы и сохранённые записи не изменяются.

## Privacy boundary

Timeline Explorer работает только с уже сохранённой network metadata. Packet payload, HTTP body, cookies, credentials, tokens и decrypted TLS content не собираются и не сохраняются.

## Скачать

Используйте `PortSentinel-0.5.6-win-x64.zip` и проверьте файл `.sha256`. Полностью распакуйте архив перед запуском.
