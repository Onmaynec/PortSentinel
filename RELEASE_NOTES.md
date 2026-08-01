# PortSentinel 0.3.0 — Session Intelligence

Новая версия превращает живой монитор в локальный центр истории и сравнения сетевой активности.

## Главное

- новая полноэкранная панель Session Intelligence;
- Live Session Recorder с сохранением уникальных TCP/UDP записей;
- локальная SQLite-база с WAL mode;
- Session History с просмотром сохранённых запусков;
- экспорт выбранной сессии в JSON или Markdown;
- Baseline Center с созданием профиля `default`;
- сравнение текущего состояния с baseline;
- отдельный экран Storage Status;
- все сетевые инструменты v0.2.0 доступны в разделе Network Tools;
- встроенное обновление через GitHub Releases сохранено.

## Хранилище

База данных создаётся локально:

```text
%LocalAppData%\PortSentinel\portsentinel.db
```

Экспортированные отчёты сохраняются в:

```text
%LocalAppData%\PortSentinel\reports
```

PortSentinel не сохраняет payload, HTTP body или расшифрованное TLS-содержимое.

## Скачать

Используйте `PortSentinel-0.3.0-win-x64.zip`. Файл `.sha256` содержит контрольную сумму архива.

Полностью распакуйте архив перед запуском.
