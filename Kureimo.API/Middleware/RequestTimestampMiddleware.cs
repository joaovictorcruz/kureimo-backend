namespace Kureimo.API.Middleware
{
    /// <summary>
    /// Captura o timestamp exato em que a request chegou ao servidor.
    /// Armazenado em HttpContext.Items para ser lido pelo ClaimController.
    /// NUNCA aceita timestamp do cliente — sempre UTC do servidor.
    /// </summary>
    public class RequestTimestampMiddleware
    {
        public const string Key = "RequestTimestamp";

        private readonly RequestDelegate _next;

        public RequestTimestampMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            context.Items[Key] = DateTimeOffset.UtcNow;
            await _next(context);
        }
    }
}
