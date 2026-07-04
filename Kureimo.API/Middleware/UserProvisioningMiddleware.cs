using Kureimo.Domain.Enums;
using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using System.Security.Claims;
using User = Kureimo.Domain.Entities.User;
namespace Kureimo.API.Middleware
{
    /// <summary>
    /// JIT provisioning: na primeira request autenticada de um "sub" do Logto
    /// que ainda não existe localmente, cria o User. Também injeta de volta
    /// duas claims que o resto do código depende: o Id local (Guid) e a Role —
    /// nenhuma das duas existe no token do Logto, são conceito de domínio nosso.
    /// </summary>
    public class UserProvisioningMiddleware
    {
        private readonly RequestDelegate _next;

        public UserProvisioningMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(
            HttpContext context,
            IUserRepository userRepository,
            IUnitOfWork unitOfWork)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var logtoId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? context.User.FindFirstValue("sub");

                if (!string.IsNullOrEmpty(logtoId))
                {
                    var user = await userRepository.GetByLogtoIdAsync(logtoId, context.RequestAborted);

                    if (user is null)
                    {
                        var email = context.User.FindFirstValue("email") ?? $"{logtoId}@sem-email.kureimo.com";

                        var phoneNumber = context.User.FindFirstValue("phoneNumber");

                        var username = context.User.FindFirstValue("username") ?? logtoId;

                        user = new User(logtoId, username, email, phoneNumber, UserRole.Collector);

                        await userRepository.AddAsync(user, context.RequestAborted);
                        await unitOfWork.CommitAsync(context.RequestAborted);
                    }

                    var identity = (ClaimsIdentity)context.User.Identity;
                    identity.AddClaim(new Claim("local_user_id", user.Id.ToString()));
                    identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
                }
            }

            await _next(context);
        }
    }
}
