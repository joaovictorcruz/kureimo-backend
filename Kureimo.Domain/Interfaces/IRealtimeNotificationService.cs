using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Interfaces
{
    public interface IRealtimeNotificationService
    {
        /// <summary>
        /// Notifica todos os clientes conectados ao grupo do set
        /// que um novo claim foi registrado.
        /// </summary>
        Task NotifyClaimRegisteredAsync(string setAccessToken, object claimPayload, CancellationToken ct = default);

        /// <summary>
        /// Notifica que o status do set mudou (ex: aberto para claims).
        /// </summary>
        Task NotifySetStatusChangedAsync(string setAccessToken, string newStatus, CancellationToken ct = default);
    }
}
