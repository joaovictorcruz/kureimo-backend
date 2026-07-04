using System.Security.Claims;

namespace Kureimo.API.Extensions
{
    public static class ClaimsExtensions
    {
        /// <summary>
        /// Extrai o UserId (Guid) do token JWT autenticado.
        /// O JWT é gerado com JwtRegisteredClaimNames.Sub que o JwtBearer mapeia
        /// automaticamente para ClaimTypes.NameIdentifier.
        /// </summary>
        public static Guid GetUserId(this ClaimsPrincipal user)
        {
            var localId = user.FindFirstValue("local_user_id");

            if (localId is null || !Guid.TryParse(localId, out var id))
                throw new InvalidOperationException("UserId local não encontrado — UserProvisioningMiddleware não rodou.");

            return id;
        }
    }
}
