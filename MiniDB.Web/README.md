# MiniDB.Web

Окремий браузерний клієнт на Blazor WebAssembly і .NET 10. Сторінка `/databases` отримує список баз через MiniDbApiClient та показує завантаження або помилку. Models містить власні HTTP DTO; посилань на інші проєкти немає.

Запуск із кореня: `.\dotnet.ps1 run --project MiniDB.Web --launch-profile http`. Адреса — `http://localhost:5180/databases`; HTTPS-профіль використовує `https://localhost:7180`.

API URL задається в `wwwroot/appsettings.json`. Для запитів до Railway потрібно оновити Server із CORS policy, яка дозволяє локальний origin. Повного GUI на цьому кроці ще немає.
