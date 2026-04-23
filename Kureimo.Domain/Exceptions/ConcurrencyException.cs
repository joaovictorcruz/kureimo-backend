using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Exceptions
{
    public class ConcurrencyException : DomainException
    {
        public ConcurrencyException()
            : base("Conflito de concorrência detectado. A operação será reprocessada.") { }
    }
}
