namespace Kureimo.API.Middleware
{
    /// <summary>
    /// Valida a API Key nos endpoints internos — acessíveis apenas pelo Worker.
    /// Rejeita qualquer request sem o header correto com 401.
    /// </summary>
    public class InternalApiKeyMiddleware
    {
        private const string HeaderName = "X-Internal-Api-Key";
        private readonly RequestDelegate _next;
        private readonly string _apiKey;

        public InternalApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _apiKey = configuration["InternalApi:ApiKey"]
                ?? throw new InvalidOperationException("InternalApi:ApiKey não configurada.");
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.TryGetValue(HeaderName, out var key) || key != _apiKey)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized.");
                return;
            }

            await _next(context);
        }
    }
}
