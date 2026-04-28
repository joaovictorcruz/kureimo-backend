using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Kureimo.API.Middleware
{
    /// <summary>
    /// Action filter para validar a API Key nos endpoints internos.
    /// Usado como [ServiceFilter] no InternalController.
    /// </summary>
    public class InternalApiKeyFilter : IActionFilter
    {
        private const string HeaderName = "X-Internal-Api-Key";
        private readonly string _apiKey;

        public InternalApiKeyFilter(IConfiguration configuration)
        {
            _apiKey = configuration["InternalApi:ApiKey"]
                ?? throw new InvalidOperationException("InternalApi:ApiKey não configurada.");
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var key)
                || key != _apiKey)
            {
                context.Result = new UnauthorizedResult();
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}
