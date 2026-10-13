# MiniDB.Server

Сервер на ASP.NET Core: бази, таблиці, додавання й редагування рядків та пошук за шаблоном. Контролери використовують Core і після кожної зміни зберігають базу через `DatabaseStorage`.

HTTP-контракти містяться в `Contracts`, файлові DTO — в `Persistence`. Значення рядків API приймає й повертає текстом; відповідь також містить індекс рядка від 0. Приклад файлового формату: [Demo.json](Persistence/Examples/Demo.json).

`GET /api/databases/{databaseName}/export` повертає JSON-файл бази. `POST /api/databases/import` приймає multipart/form-data з полем `file`. Спільний DatabaseJson використовує persistence mapper для збереження, завантаження, імпорту й експорту. Імпорт перевіряється через Core; наявна база не перезаписується, сервер повертає 409.

Шлях задається параметром `Storage:Path` в `appsettings.json` або змінною середовища `Storage__Path`. Локальне значення — `data`, відносно папки сервера. Запис відбувається через тимчасовий файл; імена баз перевіряються.

Помилки повертаються у форматі ProblemDetails: 400 для некоректних даних, 404 для відсутніх об'єктів, 409 для дублікатів. Деталі внутрішніх помилок залишаються в журналі сервера. Паралельні зміни виконуються по черзі в межах одного процесу.

CORS для Web налаштовано через `Cors:AllowedOrigins`: локально дозволені `http://localhost:5180` та `https://localhost:7180`. Policy дозволяє GET/POST/PUT/DELETE і заголовок Content-Type, зокрема для JSON та multipart/form-data; cookies не потрібні. У Railway origins можна задати змінними `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`. Щоб браузерний клієнт працював із Railway, потрібно оновити розгорнутий Server із цією policy.

Запуск із кореня проєкту: `.\dotnet.ps1 run --project MiniDB.Server`. Локальна HTTPS-адреса — `https://localhost:7143/api/databases`.

Dockerfile у корені збирає тільки Server і Core. Desktop, Tests, локальні бази та `.tools` виключені через .dockerignore. Збірка: `docker build -t minidb-server .`.

Приклад запуску в PowerShell із локальним сховищем:

```powershell
$storage = (New-Item -ItemType Directory -Path .tools/docker-data -Force).FullName
docker run --name minidb-local -d -p 8080:8080 -e PORT=8080 -e ASPNETCORE_URLS=http://+:8080 -e Storage__Path=/app/data --mount "type=bind,source=$storage,target=/app/data" minidb-server
```

`GET http://localhost:8080/health` повертає `{"status":"ok"}`. Для перевірки persistence зупиніть контейнер командою `docker stop minidb-local` і запустіть новий із іншим ім'ям та тим самим `$storage`.

Для Railway потрібні Volume у `/app/data`, змінна `Storage__Path=/app/data` і healthcheck `/health`. Сервер читає `PORT` та слухає `0.0.0.0`; явний `ASPNETCORE_URLS` або `--urls` має пріоритет. Без PORT локальні адреси не змінюються. HTTP усередині контейнера не перенаправляється на HTTPS: TLS забезпечує зовнішній proxy, а локальний HTTPS-профіль залишається доступним. Deployment на цьому етапі не виконується.
