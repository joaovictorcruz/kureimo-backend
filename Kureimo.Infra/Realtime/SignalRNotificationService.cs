using Kureimo.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Realtime
{
    /// <summary>
    /// Implementação concreta de IRealtimeNotificationService usando SignalR.
    /// O domínio e a Application conhecem apenas a interface — nunca esta classe.
    /// </summary>
    public class SignalRNotificationService : IRealtimeNotificationService
    {
        private readonly IHubContext<SetHub> _hubContext;

        public SignalRNotificationService(IHubContext<SetHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Envia o novo claim para todos os clientes conectados ao grupo do set.
        /// O front escuta o evento "ClaimRegistered" e atualiza a lista sem refresh.
        /// </summary>
        public async Task NotifyClaimRegisteredAsync(
            string setAccessToken,
            object claimPayload,
            CancellationToken ct = default)
        {
            await _hubContext.Clients
                .Group(SetHub.GetGroupName(setAccessToken))
                .SendAsync("ClaimRegistered", claimPayload, ct);
        }

        /// <summary>
        /// Notifica mudança de status do set (ex: aberto para claims).
        /// O front pode atualizar o botão de claim automaticamente.
        /// </summary>
        public async Task NotifySetStatusChangedAsync(
            string setAccessToken,
            string newStatus,
            CancellationToken ct = default)
        {
            await _hubContext.Clients
                .Group(SetHub.GetGroupName(setAccessToken))
                .SendAsync("SetStatusChanged", newStatus, ct);
        }
    }
}
