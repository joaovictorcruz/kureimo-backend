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
        public string? PhoneNumber { get; private set; }
        public string? ProfilePicUrl { get; private set; }

        private User() { }

        public User(string username, string email, string passwordHash, string phoneNumber, UserRole role = UserRole.Collector)
        {
            ValidateUsername(username);
            ValidateEmail(email);

            Username = username.Trim().ToLower();
            Email = email.Trim().ToLower();
            PasswordHash = passwordHash;
            PhoneNumber = phoneNumber.Trim();
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

        public void UpdatePhoneNumber(string phoneNumber)
        {
            ValidatePhoneNumber(phoneNumber);
            PhoneNumber = phoneNumber.Trim();
            SetUpdatedAt();
        }

        public void UpdateProfilePicUrl(string profilePicUrl)
        {
            if (string.IsNullOrWhiteSpace(profilePicUrl))
                throw new DomainException("A URL da foto de perfil não pode ser vazia.");

            ProfilePicUrl = profilePicUrl.Trim();
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

        private static void ValidatePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                throw new DomainException("O número de telefone não pode ser vazio.");

            // Aceita formatos: +5511999999999, 11999999999, (11)99999-9999
            var digits = System.Text.RegularExpressions.Regex.Replace(phoneNumber, @"\D", "");
            if (digits.Length < 10 || digits.Length > 13)
                throw new DomainException("Número de telefone inválido.");
        }

        public static void ValidatePasswordStrength(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new DomainException("A senha não pode ser vazia.");

            if (password.Length < 8)
                throw new DomainException("A senha deve ter pelo menos 8 caracteres.");

            if (!password.Any(char.IsUpper))
                throw new DomainException("A senha deve conter pelo menos uma letra maiúscula.");

            if (!password.Any(char.IsDigit))
                throw new DomainException("A senha deve conter pelo menos um número.");
        }
    }
}
