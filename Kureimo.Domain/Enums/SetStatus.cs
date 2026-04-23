using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Enums
{
    public enum SetStatus
    {
        /// <summary>GON está montando o set, ainda não visível via link.</summary>
        Draft = 0,

        /// <summary>Set publicado, link pode ser compartilhado, aguardando horário de claim.</summary>
        Published = 1,

        /// <summary>Claim aberto — usuários podem dar claim.</summary>
        Open = 2,

        /// <summary>GON encerrou o set, não aceita mais claims.</summary>
        Closed = 3
    }
}
