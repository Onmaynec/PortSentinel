# 🔄 Обновления и Releases

## Почему раньше раздел Releases был пуст

До версии 0.2.0 в репозитории находилась только документация: не существовало исполняемого проекта, release workflow, тега и готового ZIP. Обычные commits не появляются в разделе Releases автоматически.

## Автоматическая публикация версии

Источник версии — корневой файл `VERSION`.

При его изменении workflow `.github/workflows/release.yml`:

1. проверяет формат `X.Y.Z`;
2. собирает `portsentinel.exe` для Windows x64;
3. создаёт portable-папку;
4. упаковывает её в `PortSentinel-X.Y.Z-win-x64.zip`;
5. создаёт `.sha256`;
6. создаёт тег `vX.Y.Z`;
7. публикует GitHub Release и прикрепляет оба файла.

## Встроенный updater

Пункт **Update Center** обращается к официальному GitHub API:

```text
repos/Onmaynec/PortSentinel/releases/latest
```

Если версия новее текущей, программа:

1. находит ZIP для `win-x64`;
2. скачивает ZIP и файл SHA-256;
3. проверяет хэш;
4. безопасно распаковывает архив без выхода за temporary directory;
5. создаёт локальный `apply-update.cmd`;
6. закрывает текущий процесс;
7. заменяет файлы через `robocopy`;
8. запускает обновлённый `portsentinel.exe`.

Обновление выполняется только после явного подтверждения пользователя.

## Схема следующей версии

1. обновить `<Version>` в `src/PortSentinel/PortSentinel.csproj`;
2. обновить README и CHANGELOG;
3. обновить `RELEASE_NOTES.md`;
4. последним commit изменить `VERSION`;
5. дождаться зелёного workflow **Release**.

Так каждая следующая версия выпускается по одинаковой схеме.
