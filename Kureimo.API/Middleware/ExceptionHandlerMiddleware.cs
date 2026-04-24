using Kureimo.Domain.Exceptions;
using System.Net;
using System.Text.Json;

namespace Kureimo.API.Middleware
{
    /// <summary>
    /// Intercepta exceções do domínio e da aplicação e as converte em respostas HTTP adequadas.
    /// Elimina a necessidade de try/catch nos controllers.
    /// </summary>
    public class ExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;

        public ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var (statusCode, message) = exception switch
            {
                // 409 Conflict
                EmailAlreadyInUseException e => (HttpStatusCode.Conflict, e.Message),
                UsernameAlreadyInUseException e => (HttpStatusCode.Conflict, e.Message),
                UserAlreadyClaimedException e => (HttpStatusCode.Conflict, e.Message),

                // 401 Unauthorized
                InvalidCredentialsException e => (HttpStatusCode.Unauthorized, e.Message),

                // 403 Forbidden
                UnauthorizedDomainException e => (HttpStatusCode.Forbidden, e.Message),

                // 404 Not Found
                UserNotFoundException e => (HttpStatusCode.NotFound, e.Message),
                SetNotFoundException e => (HttpStatusCode.NotFound, e.Message),
                PhotocardNotFoundException e => (HttpStatusCode.NotFound, e.Message),

                // 422 Unprocessable Entity
                ClaimWindowNotOpenException e => (HttpStatusCode.UnprocessableEntity, e.Message),
                ClaimWindowClosedException e => (HttpStatusCode.UnprocessableEntity, e.Message),

                // 400 Bad Request (DomainException genérica)
                DomainException e => (HttpStatusCode.BadRequest, e.Message),

                // 500 Internal Server Error (exceções não tratadas)
                _ => (HttpStatusCode.InternalServerError, "Ocorreu um erro interno no servidor.")
            };

            if (statusCode == HttpStatusCode.InternalServerError)
                _logger.LogError(exception, "Erro não tratado na request {Method} {Path}",
                    context.Request.Method, context.Request.Path);

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var response = new { error = message };
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
