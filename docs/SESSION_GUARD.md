# ETW Session Guard

## Назначение

ETW Session Guard помогает диагностировать ситуации, когда kernel capture не запускается из-за прав, занятых logger resources или оставшейся orphan-сессии PortSentinel.

## Правила безопасности

- foreign ETW sessions только отображаются;
- автоматический stop/restart foreign sessions запрещён;
- cleanup допускает только имена `PortSentinel-*`;
- перед cleanup показывается dry-run;
- выполнение требует подтверждения `Y`;
- перед cleanup следует закрыть другие экземпляры PortSentinel.

## Guarded Capture

Guarded Capture выполняет inventory, запускает bounded capture, записывает diagnostics и сохраняет capture в SQLite. Один retry разрешён только при вероятном name collision. Любая другая ошибка приводит к snapshot fallback.

## Интерпретация failure kind

- `NotElevated` — процесс не запущен с административными правами;
- `AccessDenied` — Windows отказала в управлении ETW;
- `NameCollision` — diagnostic message похож на конфликт имени session;
- `ResourceLimit` — message указывает на нехватку logger/system resources;
- `SessionUnavailable` — target session исчезла или недоступна;
- `Unknown` — сообщение не соответствует известному безопасному классификатору.

Классификация best-effort и не заменяет Windows Event Log, ProcMon или документацию конкретного tracing tool.
