using Kureimo.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Repositories
{
    public interface IPasswordResetTokenRepository
    {
        Task<PasswordResetToken?> GetByTokenAsync(string token, CancellationToken ct = default);
        Task AddAsync(PasswordResetToken resetToken, CancellationToken ct = default);
        void Update(PasswordResetToken resetToken);
    }
}
