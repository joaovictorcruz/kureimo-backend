using Kureimo.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Repositories
{
    public interface ISetRepository
    {
        Task<Set?> GetByIdAsync(Guid id, CancellationToken ct = default);

        /// <summary>
        /// Busca o set pelo AccessToken (link único compartilhado pelo GON).
        /// </summary>
        Task<Set?> GetByAccessTokenAsync(string accessToken, CancellationToken ct = default);

        /// <summary>
        /// Busca o set com todos os photocards e claims carregados.
        /// Usado na página do set onde precisamos exibir tudo em tempo real.
        /// </summary>
        Task<Set?> GetByAccessTokenWithDetailsAsync(string accessToken, CancellationToken ct = default);

        Task<IEnumerable<Set>> GetByGonIdAsync(Guid gonId, CancellationToken ct = default);

        Task AddAsync(Set set, CancellationToken ct = default);
        void Update(Set set);
        Task<IEnumerable<Set>> GetClosedByGonIdAsync(Guid gonId, CancellationToken ct = default);
        Task SoftDeleteAllClosedByGonIdAsync(Guid gonId, CancellationToken ct = default);
    }

    public interface IPhotocardRepository
    {
        /// <summary>
        /// Busca o photocard com todos os claims já registrados.
        /// Usado na operação de claim para determinar a posição na fila.
        /// </summary>
        Task<Photocard?> GetByIdWithClaimsAsync(Guid id, CancellationToken ct = default);

        Task<Photocard?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task AddAsync(Photocard photocard, CancellationToken ct = default);
    }

    public interface IClaimRepository
    {
        Task<IEnumerable<Entities.Claim>> GetByPhotocardIdAsync(Guid photocardId, CancellationToken ct = default);
        Task<Entities.Claim?> GetByUserAndPhotocardAsync(Guid userId, Guid photocardId, CancellationToken ct = default);
        Task AddAsync(Entities.Claim claim, CancellationToken ct = default);
    }
}
