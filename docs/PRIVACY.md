# 🔏 Приватность PortSentinel

PortSentinel проектируется для анализа сетевой metadata без перехвата содержимого трафика.

## Никогда не собирается

- packet payload;
- HTTP body;
- TLS content;
- cookies, access tokens и пароли;
- содержимое файлов;
- command lines чувствительных процессов;
- данные, отключённые выбранным privacy mode.

## Режим Balanced

Планируемый режим по умолчанию:

- user name удаляется;
- `%USERPROFILE%` заменяет персональную часть пути;
- machine name не сохраняется;
- DNS сокращается до registrable domain;
- command lines отключены;
- payload не собирается.

## Режим Strict

- DNS хешируется или редактируется;
- public IP сохраняется как subnet/hash;
- host paths редактируются;
- process name и publisher могут сохраняться;
- command lines отключены.

## Sensitive processes

Для password managers, credential brokers, authentication processes, browser credential helpers, security products и protected processes применяется denylist детального сбора. Сохраняются только минимальные metadata и endpoint, без command line и чувствительных путей.

## Экспорт

Перед созданием diagnostic bundle пользователь должен видеть privacy review. Raw ETW включается только явным флагом. Импорт bundle обязан проверять checksums, schema, size limits и ZIP Slip; файлы никогда не исполняются.
