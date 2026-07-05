using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Interfaces
{
    public interface ILogtoManagementService
    {
        Task SetPrimaryEmailAsync(string logtoUserId, string email, CancellationToken ct = default);
        Task SuspendUserAsync(string logtoUserId, CancellationToken ct = default);
    }
}
