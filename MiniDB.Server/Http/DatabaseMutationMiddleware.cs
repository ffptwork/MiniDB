namespace MiniDB.Server.Http;

internal sealed class DatabaseMutationMiddleware(RequestDelegate next)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var isMutation = HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method) || HttpMethods.IsDelete(request.Method);

        if (!isMutation || !request.Path.StartsWithSegments("/api/databases"))
        {
            await next(context);
            return;
        }

        // Захищає весь Load → Change → Save у межах одного процесу сервера.
        await _gate.WaitAsync(context.RequestAborted);
        try
        {
            await next(context);
        }
        finally
        {
            _gate.Release();
        }
    }
}
