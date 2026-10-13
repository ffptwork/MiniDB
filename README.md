# MiniDB

Навчальна таблична СУБД, тип 1, варіант 01. Індивідуальна операція — пошук рядків за шаблоном у вибраній колонці.

- **MiniDB.Core** — моделі, типи даних, перевірки та пошук.
- **MiniDB.Server** — REST API та зберігання баз у JSON.
- **MiniDB.Desktop** — WPF-інтерфейс для роботи з базами, таблицями, рядками та пошуком через REST API.
- **MiniDB.Web** — окремий Blazor WebAssembly-клієнт; наразі показує список баз через REST API.
- **MiniDB.Tests** — тести ядра, файлового сховища, HTTP API, desktop API-клієнта та ViewModels.

Клієнти працюють із сервером через HTTP/HTTPS REST. Сервер використовує Core; ядро не залежить від інтерфейсу чи файлового сховища.

Для повної збірки потрібні Windows і .NET 10 SDK. Поки SDK зберігається локально в `.tools`, команди з кореня проєкту такі:

```powershell
.\dotnet.ps1 build MiniDB.sln
.\dotnet.ps1 test MiniDB.sln
```

Для локальної роботи запустіть `.\dotnet.ps1 run --project MiniDB.Server --no-launch-profile -- --urls http://localhost:5143`, потім в іншому терміналі `.\dotnet.ps1 run --project MiniDB.Desktop`. Адреса сервера задається в `MiniDB.Desktop/appsettings.json`, локально — `http://localhost:5143/`.

Після встановлення системного .NET 10 можна користуватися звичайними командами `dotnet`.

