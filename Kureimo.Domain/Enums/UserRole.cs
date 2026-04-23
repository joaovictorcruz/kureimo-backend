using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Enums
{
    public enum UserRole
    {
        /// <summary>Usuário padrão — pode dar claim em photocards.</summary>
        Collector = 0,

        /// <summary>Group Order Manager — pode criar e gerenciar sets.</summary>
        Gon = 1,

        /// <summary>Administrador da plataforma.</summary>
        Admin = 2
    }
}
