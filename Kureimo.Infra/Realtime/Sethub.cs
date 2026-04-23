using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Realtime
{
    /// <summary>
    /// Hub SignalR do set. Cada set tem seu próprio grupo identificado pelo AccessToken.
    ///
    /// Fluxo do cliente:
    ///   1. Conecta ao hub: /hubs/set
    ///   2. Chama JoinSet(accessToken) para entrar no grupo do set específico
    ///   3. Recebe eventos em tempo real: "ClaimRegistered", "SetStatusChanged"
    ///   4. Ao sair da página, chama LeaveSet(accessToken) ou a conexão encerra automaticamente
    /// </summary>
    public class SetHub : Hub
    {
        /// <summary>
        /// Cliente entra no grupo do set para receber notificações em tempo real.
        /// Chamado pelo front quando o usuário abre a página do set.
        /// </summary>
        public async Task JoinSet(string accessToken)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(accessToken));
        }

        /// <summary>
        /// Cliente sai do grupo. Chamado quando a janela de claim fecha
        /// ou o usuário navega para fora da página.
        /// </summary>
        public async Task LeaveSet(string accessToken)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(accessToken));
        }

        /// <summary>
        /// Nome padronizado do grupo SignalR para um set.
        /// </summary>
        public static string GetGroupName(string accessToken) => $"set:{accessToken}";
    }
}
