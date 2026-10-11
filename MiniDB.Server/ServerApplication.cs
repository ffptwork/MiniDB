using MiniDB.Core.Search;
using MiniDB.Core.Validation;
using MiniDB.Server.Http;
using MiniDB.Server.Persistence;

namespace MiniDB.Server;

public static class ServerApplication
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(ServerApplication).Assembly.GetName().Name
        });

        var port = Environment.GetEnvironmentVariable("PORT");
        if (!string.IsNullOrWhiteSpace(port) && string.IsNullOrWhiteSpace(builder.Configuration["urls"]))
        {
            if (!int.TryParse(port, out var number) || number is < 1 or > 65535)
                throw new InvalidOperationException("PORT має бути числом від 1 до 65535.");
            builder.WebHost.UseUrls($"http://0.0.0.0:{number}");
        }

        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<ApiExceptionHandler>();
        builder.Services.AddSingleton(serviceProvider =>
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var environment = serviceProvider.GetRequiredService<IHostEnvironment>();
            var path = configuration["Storage:Path"]
                ?? throw new InvalidOperationException("Не задано Storage:Path.");
            return new DatabaseStorage(Path.GetFullPath(path, environment.ContentRootPath));
        });
        builder.Services.AddSingleton<TableValidator>();
        builder.Services.AddSingleton<RowValidator>();
        builder.Services.AddSingleton<SearchService>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseMiddleware<DatabaseMutationMiddleware>();
        app.MapControllers();
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        return app;
    }
}
