# MiniDB.Tests

Тести на xUnit для Core, JSON-сховища, REST API та desktop API-клієнта. Проєкт має посилання на Core, Server і Desktop та ціль net10.0-windows для перевірки WPF-проєкту.

Файлові й API-тести використовують тимчасові папки. Для HTTP-перевірок запускається Kestrel на випадковому локальному порту. Тести MiniDbApiClient підставляють HttpMessageHandler і не звертаються до мережі. Папка застосунку `data` не використовується.

Запуск із кореня проєкту: `.\dotnet.ps1 test MiniDB.Tests\MiniDB.Tests.csproj`.
