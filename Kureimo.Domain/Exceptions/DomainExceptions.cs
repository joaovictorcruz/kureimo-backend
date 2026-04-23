using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Exceptions
{
    public class ClaimWindowNotOpenException : DomainException
    {
        public ClaimWindowNotOpenException()
            : base("O horário de claim ainda não foi atingido.") { }
    }

    public class ClaimWindowClosedException : DomainException
    {
        public ClaimWindowClosedException()
            : base("A janela de claim para este set foi encerrada.") { }
    }

    public class UserAlreadyClaimedException : DomainException
    {
        public UserAlreadyClaimedException()
            : base("Você já deu claim neste photocard.") { }
    }

    public class SetNotFoundException : DomainException
    {
        public SetNotFoundException()
            : base("Set não encontrado.") { }

        public SetNotFoundException(string accessToken)
            : base($"Set com token '{accessToken}' não encontrado.") { }
    }

    public class PhotocardNotFoundException : DomainException
    {
        public PhotocardNotFoundException()
            : base("Photocard não encontrado.") { }

        public PhotocardNotFoundException(Guid id)
            : base($"Photocard '{id}' não encontrado.") { }
    }

    public class UserNotFoundException : DomainException
    {
        public UserNotFoundException()
            : base("Usuário não encontrado.") { }
    }

    public class UnauthorizedDomainException : DomainException
    {
        public UnauthorizedDomainException()
            : base("Você não tem permissão para realizar esta ação.") { }
    }

    public class EmailAlreadyInUseException : DomainException
    {
        public EmailAlreadyInUseException()
            : base("Este e-mail já está em uso.") { }
    }

    public class UsernameAlreadyInUseException : DomainException
    {
        public UsernameAlreadyInUseException()
            : base("Este username já está em uso.") { }
    }

    public class InvalidCredentialsException : DomainException
    {
        public InvalidCredentialsException()
            : base("Credenciais inválidas.") { }
    }
}
