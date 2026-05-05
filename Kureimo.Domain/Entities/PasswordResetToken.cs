using Kureimo.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Entities
{
    public class PasswordResetToken : BaseEntity
    {
        public Guid UserId { get; private set; }
        public string Token { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        public DateTimeOffset? UsedAt { get; private set; }

        public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
        public bool IsUsed => UsedAt.HasValue;
        public bool IsValid => !IsExpired && !IsUsed;

        private PasswordResetToken() { }

        public PasswordResetToken(Guid userId)
        {
            UserId = userId;
            Token = GenerateToken();
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        }

        public void MarkAsUsed()
        {
            if (!IsValid)
                throw new DomainException("Token inválido ou já utilizado.");

            UsedAt = DateTimeOffset.UtcNow;
            SetUpdatedAt();
        }

        private static string GenerateToken()
        {
            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}
