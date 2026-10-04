# MiniDB.Core

Независимое ядро MiniDB, вариант 01. Не зависит от WPF, ASP.NET Core или файлового хранилища. Не использует сторонние пакеты.

## Структура и ответственность классов

```text
MiniDB.Core/
  MiniDB.Core.csproj
  README.md
  Models/
    Database.cs
    Table.cs
    Column.cs
    Row.cs
    Color.cs
    ColorInterval.cs
  DataTypes/
    IDataType.cs
    DataType.cs
    IntegerType.cs
    RealType.cs
    CharType.cs
    StringType.cs
    ColorType.cs
    ColorIntervalType.cs
  Validation/             пока только .gitkeep
  Search/                 пока только .gitkeep
```

| Класс / интерфейс | Ответственность |
| --- | --- |
| Database | Имя базы, коллекция таблиц, добавление, удаление и получение таблицы по имени. |
| Table | Имя, схема и строки; добавление колонок, добавление и редактирование строк, получение колонки и строк. |
| Column | Неизменяемые имя колонки и её IDataType. |
| Row | Типизированные значения по именам колонок; чтение и изменение ячеек с проверкой типа. |
| Color | Неизменяемые Red, Green, Blue, каждая в диапазоне 0..255. |
| ColorInterval | Неизменяемые MinColor и MaxColor, упорядоченные по каждой RGB-компоненте. |
| IDataType | Общий контракт Name, Validate(string?), Parse(string). |
| DataType<T> | Одна процедура TryParse для Validate и Parse; единые ошибки преобразования. |
| IntegerType | `integer`: 32-битный int, включая отрицательные числа. |
| RealType | `real`: конечный double с InvariantCulture; точка, допускается экспоненциальная запись. |
| CharType | `char`: ровно один C# char (UTF-16 code unit). |
| StringType | `string`: любая ненулевая строка, включая пустую, без обрезания пробелов. |
| ColorType | `color`: формат RGB(r, g, b); общий разбор RGB и проверка диапазонов. |
| ColorIntervalType | `colorInvl`: формат [RGB(...); RGB(...)]; переиспользует разбор ColorType и проверяет порядок границ. |

## Пример

```csharp
using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

var database = new Database("Demo");
var table = new Table("Items");
table.AddColumn(new Column("Id", new IntegerType()));
table.AddColumn(new Column("Color", new ColorType()));
database.AddTable(table);

var row = table.AddRow(new[] { "1", "RGB(255, 0, 0)" });
var color = (Color)row.GetValue("Color");
row.SetValue("Color", "RGB(0, 255, 0)");
table.EditRow(0, new[] { "2", "RGB(0, 0, 255)" });
```

## Архитектурные решения

- Самих UML-файлов в рабочей папке нет; реализация следует списку моделей и методов из задания. Дополнительные детали реализации описаны ниже.
- Общий базовый класс DataType<T> устраняет расхождения между Validate и Parse. Внешний контракт остаётся IDataType; Parse возвращает object с конкретным типом значения.
- Row получает схему при создании и хранит Values как словарь `имя колонки -> типизированное значение`. SetValue принимает текст и проверяет его через IDataType колонки. Это защищает таблицу от неверных типов при прямом изменении Row.
- AddRow и EditRow принимают текстовые значения в порядке Columns. Количество значений должно точно совпадать с количеством колонок. EditRow использует индекс с нуля и сохраняет тот же объект Row.
- Новая строка добавляется, а редактирование существующей применяется только после успешного разбора всех значений. Ошибка оставляет прежнее состояние.
- Колонки можно добавлять только до появления строк. Причина: значения по умолчанию и миграция схемы ещё не определены. Это ограничение объявлено перед реализацией.
- Коллекции доступны только для чтения; все изменения идут через методы моделей. Имена, Column, Color и ColorInterval неизменяемы.
- Имена не могут быть пустыми или состоять только из пробелов. Дубликаты таблиц и колонок запрещены; сравнение имён регистрозависимое (Ordinal), без автоматического обрезания пробелов.
- RGB-парсер общий для обоих цветовых типов; диапазон компоненты определяется в одном методе Color.IsValidComponent. Порядок границ определяется в ColorInterval.IsOrdered. Конструкторы моделей также защищают их корректность при прямом создании.
- RGB записывается с заглавными буквами `RGB`; вокруг значений, цветов и интервала допускаются пробельные символы.
- RealType отклоняет NaN, бесконечности и переполнение. CharType представляет C# char; emoji из двух UTF-16 code units не считается одним char. Null не является значением ни одного типа; пустая строка допустима для StringType.
- GetRows возвращает все строки. Поиск и SearchService пока не реализованы; отдельные классы валидаторов тоже не нужны для текущего объёма.

## Ошибки

- Validate возвращает false для неверного текста и null.
- Parse выбрасывает FormatException для неверного текста, ArgumentNullException для null.
- Неизвестное имя таблицы или колонки: KeyNotFoundException. RemoveTable возвращает false, если таблицы нет.
- Неверное имя, дубликат или количество значений: ArgumentException. Неверный индекс строки или RGB-компонента при прямом создании: ArgumentOutOfRangeException.
- Изменение схемы заполненной таблицы или добавление строки без колонок: InvalidOperationException.

## Проверка

Из папки solution:

```powershell
.\dotnet.ps1 build MiniDB.sln
.\dotnet.ps1 test MiniDB.Tests\MiniDB.Tests.csproj --no-restore
```

В MiniDB.Tests добавлены проверки числовых границ и культуры, всех примеров RGB, границ и порядка интервалов, типизированных строк, атомарности изменения и защиты коллекций. Server и Desktop не изменялись. JSON, DatabaseStorage, REST API и SearchService в Core отсутствуют.
