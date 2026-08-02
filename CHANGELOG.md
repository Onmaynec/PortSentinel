# Changelog

Все значимые изменения PortSentinel фиксируются в этом файле.

## [0.5.6] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Timeline Explorer;
- server-side pagination capture index и event timeline через SQLite `LIMIT/OFFSET`;
- динамический page size на основе высоты терминала;
- навигация PageUp/PageDown и Home/End;
- preset-фильтры event kind и protocol family;
- параметризованный text search по process name, IP addresses, ports и diagnostic note;
- переход к точному event sequence с вычислением страницы без полной загрузки capture;
- JSON schema v1 и Markdown export только текущей SQL-page;
- backward-compatible индексы по capture/sequence, capture/kind и capture/protocol;
- полный Network Coverage v0.5.5 сохранён во вложенной панели.

### Масштабирование и безопасность

- Timeline Explorer не загружает все events capture в память;
- пользовательский search text передаётся SQLite только через parameters;
- `%`, `_` и `\\` экранируются как literal LIKE characters;
- page export явно содержит только текущий отображаемый диапазон;
- таблицы и существующие archive records не изменяются;
- packet payload, HTTP body, credentials, tokens и decrypted TLS content не сохраняются.

## [0.5.5] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Network Coverage;
- kernel ETW callbacks для TCP IPv6 connect, accept, disconnect, retransmit и reconnect;
- UDP IPv4 и IPv6 send/receive callbacks;
- нормализация протоколов `TCP4`, `TCP6`, `UDP4` и `UDP6`;
- 15-секундный Coverage Capture с автоматическим сохранением в SQLite;
- coverage-анализ последней или выбранной архивной capture-сессии;
- protocol matrix с количеством events, процессов, remote endpoints и directions;
- распределение IPv4/IPv6 и TCP/UDP;
- top remote endpoints;
- JSON schema v1 и Markdown export coverage reports;
- schema v3 для обычного ETW export с UDP и IPv6 counters;
- полный Connection Health v0.5.4 сохранён во вложенной панели.

### Исправлено

- удалён повторный byte-swap ETW-портов: TraceEvent уже возвращает port values в host byte order.

### Безопасность и ограничения

- UDP source port может отсутствовать в выбранных kernel callbacks и тогда сохраняется как `0`;
- отсутствие protocol family в bounded capture не доказывает отсутствие трафика;
- максимум 5000 нормализованных событий сохраняется на capture;
- packet payload, HTTP body, cookies, credentials, tokens и decrypted TLS content не собираются.

## [0.5.4] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Connection Health;
- kernel ETW callbacks `TcpIpFail` и `TcpIpReconnect`;
- сохранение numeric failure code и protocol как evidence без speculative decoding;
- live Capture & Health с автоматическим сохранением результата в SQLite;
- health-анализ последней или выбранной архивной capture-сессии;
- explainable findings для kernel failures, retransmit bursts, reconnect loops и rapid repeated connections;
- capture-boundary finding для disconnect без наблюдаемого connect;
- явное limitation для SnapshotFallback, где kernel lifecycle events недоступны;
- health score 0–100 и уровни Stable / Observe / Degraded / Critical;
- JSON schema v1 и Markdown export health reports;
- полный Archive Operations v0.5.3 сохранён во вложенной панели.

### Безопасность и ограничения

- health score является диагностическим summary, а не malware verdict;
- numeric kernel failure codes сохраняются без недокументированной расшифровки;
- retransmit и reconnect patterns могут быть вызваны обычной потерей пакетов, roaming, proxy или retry logic;
- анализ ограничен capture window;
- packet payload, HTTP body, cookies, credentials, tokens и decrypted TLS content не собираются.

## [0.5.3] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Archive Operations;
- capture profiles на 5, 15, 30 и 60 секунд;
- автоматическое сохранение profile captures в существующий telemetry archive;
- параметризованный поиск по process name, IP addresses и diagnostic notes;
- preset-фильтры retransmit, disconnect, snapshot fallback и listeners;
- выбор произвольной пары из последних 50 captures для lifecycle comparison;
- Archive Status с количеством captures/events, диапазоном дат и размером SQLite-файла;
- retention policies для сохранения последних 25, 50, 100 или 250 captures;
- обязательный dry-run preview перед удалением;
- каскадная очистка связанных telemetry events в одной транзакции;
- полный Telemetry Archive v0.5.2 сохранён во вложенной панели.

### Безопасность и совместимость

- retention удаляет только `telemetry_captures` и связанные `telemetry_events`;
- sessions, baselines и обычные reports не затрагиваются;
- поиск использует параметры SQLite, а не строковую сборку SQL;
- очистка выполняется только после явного подтверждения клавишей `Y`;
- packet payload, HTTP body, cookies, credentials, tokens и decrypted TLS content не сохраняются.

## [0.5.2] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Telemetry Archive;
- автоматическое сохранение ETW и snapshot fallback captures в SQLite;
- таблицы `telemetry_captures` и `telemetry_events`;
- транзакционное сохранение capture header и всех event records;
- Telemetry History для 100 последних capture-сессий;
- просмотр сохранённых event metadata;
- JSON schema v1 и Markdown export архивных captures;
- сравнение двух последних captures по lifecycle fingerprint без PID;
- отображение новых событий и исчезнувших fingerprints;
- JSON/Markdown export telemetry comparison;
- полный ETW Control Center v0.5.1 сохранён во вложенной панели.

### Совместимость и приватность

- новые таблицы создаются через `CREATE TABLE IF NOT EXISTS`;
- существующие sessions, baselines и reports не изменяются;
- lifecycle fingerprint является диагностикой и не формирует threat verdict;
- packet payload, HTTP body, cookies, credentials, tokens и decrypted TLS content не сохраняются.

## [0.5.1] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель ETW Telemetry;
- read-only kernel ETW backend на базе `Microsoft.Diagnostics.Tracing.TraceEvent`;
- TCP IPv4 события connect, accept, disconnect и retransmit;
- 12-секундное окно capture с просмотром event metadata;
- capability probe для Windows elevation и управления kernel session;
- автоматический snapshot fallback через Windows IP Helper API;
- экспорт ETW capture в JSON schema v1 и Markdown;
- отдельные статусы backend, fallback reason и privacy boundary.

### Безопасность и ограничения

- PortSentinel не включает packet capture и не сохраняет payload;
- HTTP body, cookies, tokens и decrypted TLS content не собираются;
- управление kernel ETW обычно требует запуска от администратора;
- при недостаточных правах, конфликте системной сессии или другой ошибке используется snapshot fallback;
- первый ETW vertical slice обрабатывает TCP IPv4; IPv6/UDP provider coverage перенесено в 0.5.x.

## [0.5.0] — 2026-08-02

### Добавлено

- новая верхнеуровневая панель Extended Telemetry;
- Application Watch для выбранного сетевого процесса;
- timeline first seen / last seen и число наблюдений endpoint;
- подсчёт connection cycles и выделение reconnect loops;
- автоматический экспорт Application Watch в JSON и Markdown;
- best-effort reverse DNS correlation с timeout, ограничением параллелизма и локальным кэшем;
- Network Process Tree через Windows Toolhelp32 API;
- отображение родительских процессов для процессов с сетевой активностью;
- сравнение двух последних сохранённых сессий по стабильному fingerprint;
- diff новых/исчезнувших endpoints и процессов;
- экспорт session diff в JSON и Markdown;
- все возможности v0.4.0 сохранены в отдельном Control Center.

### Безопасность и ограничения

- DNS correlation не считается доказательством владельца трафика;
- Application Watch использует периодические Windows network snapshots и не перехватывает payload;
- reconnect loop определяется по повторным появлениям одинакового process/remote endpoint;
- ETW backend перенесён в стабилизационную ветку 0.5.x и не требуется для запуска v0.5.0.

## [0.4.0] — 2026-08-01

### Добавлено

- новый экран Explainable Rules;
- `NewListenerRule`, `WildcardListenerRule`, `UnsignedNetworkProcessRule` и `TempDirectoryNetworkProcessRule`;
- severity, confidence, evidence и limitations;
- SHA-256 и Authenticode enrichment;
- стабильный baseline fingerprint без PID.

## [0.3.0] — 2026-08-01

- SQLite sessions и WAL;
- Live Session Recorder и Session History;
- JSON/Markdown exports;
- Baseline Center и portable data directory.

## [0.2.0] — 2026-08-01

- самостоятельный .NET 8 Windows executable;
- полноэкранная TUI;
- TCP/UDP IPv4/IPv6 через Windows IP Helper API;
- process mapping, Quick Scan и GitHub Releases updater.

## [0.1.0] — 2026-08-01

- первоначальная архитектура и документация проекта.
