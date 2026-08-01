# 🤝 Участие в разработке PortSentinel

Спасибо за интерес к проекту. PortSentinel находится на ранней архитектурной стадии, поэтому сейчас особенно полезны обсуждения Windows Internals, TCP/IP, ETW, Windows Firewall, безопасного UX и тестируемости.

## Перед началом работы

1. Проверьте существующие Issues и Pull Requests.
2. Для крупной функции сначала создайте Issue с целями, границами и рисками.
3. Не начинайте массовую реализацию ETW, Firewall или enrichment до готовности первого TCP vertical slice.

## Технические правила

- C# / .NET 8, nullable reference types;
- async API и `CancellationToken`;
- native resources освобождаются через `SafeHandle`;
- Win32 errors проверяются;
- IPv4 и IPv6 поддерживаются одинаково корректно;
- PID reuse учитывается через process identity;
- SQL только parameterized;
- HTML output экранируется;
- import валидируется и защищён от ZIP Slip;
- UI отделён от core;
- rules не имеют side effects;
- пустые `catch` и критические `TODO` не допускаются;
- warnings собственного кода считаются errors.

## Проверки перед Pull Request

```powershell
dotnet restore
dotnet build
dotnet test
```

Для changes в native telemetry нужны unit tests через mock adapters. Unit tests не должны зависеть от реальной сети. Elevated Firewall tests выполняются отдельным job и меняют только тестовые managed rules PortSentinel.

## Commit и PR

Используйте короткие понятные commit messages. В PR опишите, что изменено, почему, как проверено, какие ограничения остались и затрагивает ли изменение privacy/security model.
