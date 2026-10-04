# MiniDB — Етап 2

Навчальний проєкт: система типу 1, варіант 01. Реалізовано MiniDB.Core: моделі, шість типів даних і перевірки значень. Server та Desktop залишаються початковим каркасом.

## Узгоджений контекст

- Стек: .NET 10, C#, WPF, ASP.NET Core Web API, xUnit.
- Архітектура: `MiniDB.Desktop -> HTTPS/REST -> MiniDB.Server -> MiniDB.Core`.
- Обов'язкові типи: `integer`, `real`, `char`, `string`.
- Додаткові типи: `color` (RGB), `colorInvl` (інтервал між двома RGB-кольорами).
- Індивідуальна операція: пошук рядків таблиці за шаблоном.
- Майбутній `DatabaseStorage` працюватиме лише на сервері та зберігатиме JSON-файли у Railway Volume.
- Етап 3 з `MiniDB.Web` на C#/Blazor не входить у поточну реалізацію.
- У Core реалізовано Database, Table, Column, Row, Color, ColorInterval та всі шість типів. Деталі й архітектурні рішення: [MiniDB.Core/README.md](MiniDB.Core/README.md).
- Пошук, сховище, JSON, API й розгортання ще не реалізовано.

## Проєкти та references

| Проєкт | Призначення | ProjectReference |
| --- | --- | --- |
| MiniDB.Core | Незалежне ядро, `net10.0` | Немає |
| MiniDB.Server | ASP.NET Core Web API, `net10.0` | MiniDB.Core |
| MiniDB.Desktop | WPF-клієнт, `net10.0-windows` | Немає |
| MiniDB.Tests | xUnit, `net10.0` | MiniDB.Core |

Desktop взаємодіятиме із сервером через HTTP API; прямої залежності від серверного проєкту та сховища немає. Усі проєкти мають увімкнені nullable та implicit usings. Пакети додано лише до тестового проєкту: xUnit, його адаптер та Microsoft.NET.Test.Sdk. Порожні папки збережено через `.gitkeep`.

```text
MiniDB.sln
MiniDB.Core/
  MiniDB.Core.csproj
  Models/
  DataTypes/
  Validation/
  Search/
MiniDB.Server/
  MiniDB.Server.csproj
  Program.cs
  appsettings.json
  appsettings.Development.json
  Properties/launchSettings.json
  Controllers/
  Persistence/
  Contracts/
MiniDB.Desktop/
  MiniDB.Desktop.csproj
  App.xaml
  App.xaml.cs
  Views/MainWindow.xaml
  Views/MainWindow.xaml.cs
  ViewModels/
  Services/
  Models/
MiniDB.Tests/
  MiniDB.Tests.csproj
  DataTypes/
  Validation/
  Search/
global.json
.gitignore
README.md
BUILD-RESULT.md
dotnet.ps1
```

## Збірка та запуск

Потрібні Windows і .NET 10 SDK. `global.json` дозволяє стабільні SDK лінійки 10.0; .NET 8 та 9 не підходять. У папці `.tools/dotnet` уже є локальний SDK 10.0.401, а `.tools/nuget` містить потрібні пакети для тестового проєкту. Скрипт `dotnet.ps1` використовує ці локальні файли та не змінює системний SDK. Команди виконуються з папки solution:

```powershell
.\dotnet.ps1 build MiniDB.sln
.\dotnet.ps1 run --project MiniDB.Server --launch-profile https
.\dotnet.ps1 run --project MiniDB.Desktop
.\dotnet.ps1 test MiniDB.sln
```

Сервер налаштовано на `https://localhost:7143` та `http://localhost:5143`. Для локального HTTPS потрібен development certificate; за потреби виконайте `.\dotnet.ps1 dev-certs https --trust`. Контролери ще не реалізовані. Desktop містить порожнє базове WPF-вікно. У MiniDB.Tests додано 98 перевірок Core; пошук поки не тестується, оскільки ще не реалізований.

