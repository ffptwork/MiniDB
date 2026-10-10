# MiniDB.Desktop

Клієнт на WPF для Windows. `MiniDbApiClient` у Services виконує REST-запити до сервера. DTO містяться в Models; посилання на серверний проєкт немає. Вікно залишається базовим, без роботи з таблицями.

Адреса задається при створенні `HttpClient` і має завершуватися `/`. Наприклад, для локальної розробки:

```csharp
using var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5143/") };
var api = new MiniDbApiClient(httpClient);
var databases = await api.GetDatabasesAsync();
```

Клієнт використовує переданий HttpClient; його життєвим циклом керує викликач. API-помилки повертаються як `ApiException` з HTTP-статусом, Title, Detail та Errors. Скасування запиту і мережеві помилки передаються викликачу. Діалогові вікна сервіс не відкриває.
