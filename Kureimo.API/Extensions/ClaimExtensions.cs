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
            var sub = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (sub is null || !Guid.TryParse(sub, out var id))
                throw new InvalidOperationException("UserId não encontrado no token JWT.");

            return id;
        }
    }
}
