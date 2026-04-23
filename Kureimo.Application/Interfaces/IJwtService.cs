using Kureimo.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateToken(User user);
    }
}
