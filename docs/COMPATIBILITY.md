# 🪟 Совместимость

| Компонент | Требование |
|---|---|
| Операционная система | Windows 10 x64 или Windows 11 x64 |
| Архитектура Release | x64 |
| Среда выполнения | Встроена в self-contained Release |
| Терминал | Windows Terminal, современный CMD или PowerShell console host |
| Права | Обычный пользователь; administrator расширяет доступ к process metadata |
| Сеть | Нужна только для проверки и установки обновлений |

PortSentinel использует Windows IP Helper API из `iphlpapi.dll`. Другие операционные системы намеренно завершаются с exit code `16`.
