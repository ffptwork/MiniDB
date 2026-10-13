# MiniDB.Web

Окремий браузерний клієнт на Blazor WebAssembly і .NET 10. На `/databases` можна працювати з базами, таблицями й рядками, шукати за шаблоном та імпортувати/експортувати JSON. Усі операції виконує MiniDbApiClient через REST API; Models містить власні HTTP DTO, посилань на інші проєкти немає.

Запуск із кореня: `.\dotnet.ps1 run --project MiniDB.Web --launch-profile http`. Адреса — `http://localhost:5180/databases`; HTTPS-профіль використовує `https://localhost:7180`.

API URL задається в `wwwroot/appsettings.json`. Server має дозволяти origin Web через CORS. DatabaseWorkspace керує вибором і станами запитів; Razor-компоненти будують поля за схемою таблиці. Імпорт обмежений 10 МіБ та перевіряється сервером. Експорт завантажується через невеликий JS-модуль без розбору persistence-формату у Web.
