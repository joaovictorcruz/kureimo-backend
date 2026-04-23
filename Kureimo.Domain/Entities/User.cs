using Kureimo.Domain.Enums;
using Kureimo.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Entities
{
    public class User : BaseEntity
    {
        public string Username { get; private set; }
        public string Email { get; private set; }
        public string PasswordHash { get; private set; }
        public UserRole Role { get; private set; }
        public bool IsActive { get; private set; }

        private User() { }

        public User(string username, string email, string passwordHash, UserRole role = UserRole.Collector)
        {
            ValidateUsername(username);
            ValidateEmail(email);

            Username = username.Trim().ToLower();
            Email = email.Trim().ToLower();
            PasswordHash = passwordHash;
            Role = role;
            IsActive = true;
        }

        public void UpdateUsername(string username)
        {
            ValidateUsername(username);
            Username = username.Trim().ToLower();
            SetUpdatedAt();
        }

        public void UpdateEmail(string email)
        {
            ValidateEmail(email);
            Email = email.Trim().ToLower();
            SetUpdatedAt();
        }

        public void UpdatePasswordHash(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
                throw new DomainException("Password hash não pode ser vazio.");

            PasswordHash = passwordHash;
            SetUpdatedAt();
        }

        public void PromoteToGon()
        {
            if (Role == UserRole.Gon)
                throw new DomainException("Usuário já é um GON.");

            Role = UserRole.Gon;
            SetUpdatedAt();
        }

        public void Deactivate()
        {
            if (!IsActive)
                throw new DomainException("Usuário já está inativo.");

            IsActive = false;
            SetUpdatedAt();
        }

        public void Activate()
        {
            if (IsActive)
                throw new DomainException("Usuário já está ativo.");

            IsActive = true;
            SetUpdatedAt();
        }

        private static void ValidateUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new DomainException("Username não pode ser vazio.");

            if (username.Trim().Length < 3)
                throw new DomainException("Username deve ter no mínimo 3 caracteres.");

            if (username.Trim().Length > 30)
                throw new DomainException("Username deve ter no máximo 30 caracteres.");
        }

        private static void ValidateEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("Email não pode ser vazio.");

            if (!email.Contains('@'))
                throw new DomainException("Email inválido.");
        }
    }
}
