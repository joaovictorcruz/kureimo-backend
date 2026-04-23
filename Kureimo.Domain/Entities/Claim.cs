using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Entities
{
    public class Claim : BaseEntity
    {
        public Guid PhotocardId { get; private set; }
        public Guid UserId { get; private set; }

        /// Timestamp exato capturado no servidor no momento da request.
        /// NUNCA aceita valor vindo do cliente.
        public DateTimeOffset ClaimedAt { get; private set; }

        /// Posição na fila de claims para este photocard.
        /// Posição 1 = primeiro a dar claim.
        public int QueuePosition { get; private set; }

        // EF Core constructor
        private Claim() { }

        /// Construtor interno — só o Photocard pode criar um Claim (via RegisterClaim).
        internal Claim(Guid photocardId, Guid userId, DateTimeOffset serverTimestamp, int position)
        {
            PhotocardId = photocardId;
            UserId = userId;
            ClaimedAt = serverTimestamp;
            QueuePosition = position;
        }
    }

}
