# Результат проверки MiniDB.Core

Дата: 4 октября 2026 года. Рабочая папка: `D:\Study\University\IT\progect`.

Реализованы модели Database, Table, Column, Row, Color, ColorInterval и типы integer, real, char, string, color, colorInvl. Архитектурные решения и ответственность классов описаны в [MiniDB.Core/README.md](MiniDB.Core/README.md).

## Зависимости и область изменения

- MiniDB.Core остаётся независимой библиотекой net10.0 с nullable и implicit usings.
- Новых NuGet-пакетов и новых project references нет.
- Добавлены только реализация Core, тесты Core в существующем MiniDB.Tests и актуализированная документация.
- Проверены контрольные суммы 16 исходных файлов Server/Desktop: они совпадают с исходным каркасом.
- DatabaseStorage, JSON, SearchService и API не реализовывались.

## Зборка

Локальный .NET SDK 10.0.401. Из корня solution выполнено:

```powershell
.\dotnet.ps1 build MiniDB.sln
```

```text
MiniDB.Core    -> MiniDB.Core.dll
MiniDB.Server  -> MiniDB.Server.dll
MiniDB.Desktop -> MiniDB.Desktop.dll
MiniDB.Tests   -> MiniDB.Tests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)
Exit code: 0
```

Полный вывод: `.tools/core-build.log`.

## Тесты

```powershell
.\dotnet.ps1 test MiniDB.Tests\MiniDB.Tests.csproj --no-restore --logger 'console;verbosity=normal'
```

Результат: 98 проверок, 98 успешно, 0 неудачных; код завершения 0. Параметризованные примеры считаются отдельными проверками. Полный вывод: `.tools/core-tests.log`.

Проверены все примеры цветов из задания, диапазоны каждой компоненты RGB, порядок каждого компонента границ интервала, числовые пределы, InvariantCulture, null и пустые значения, дубликаты имён, типы значений строк, количество значений, границы индексов, защита коллекций и отсутствие частичного изменения при ошибке.

Текущий шаг не включал запуск Server или Desktop.
