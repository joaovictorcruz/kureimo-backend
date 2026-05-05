using Kureimo.Application.DTOs;
using Kureimo.Application.Interfaces;
using Kureimo.Domain.Entities;
using Kureimo.Domain.Enums;
using Kureimo.Domain.Exceptions;
using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.Services
{
    public class AuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtService _jwtService;
        private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
        private readonly IEmailService _emailService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtService jwtService,
            IPasswordResetTokenRepository passwordResetTokenRepository,
            IEmailService emailService,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _passwordResetTokenRepository = passwordResetTokenRepository;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<(AuthResponseDto dto, string token)> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
        {
            // Verifica unicidade antes de criar a entidade
            // (regra de unicidade é de infra, não do domínio puro)
            if (await _userRepository.EmailExistsAsync(dto.Email, ct))
                throw new EmailAlreadyInUseException();

            if (await _userRepository.UsernameExistsAsync(dto.Username, ct))
                throw new UsernameAlreadyInUseException();

            User.ValidatePasswordStrength(dto.Password);

            var passwordHash = _passwordHasher.Hash(dto.Password);
            var role = dto.IsGon ? UserRole.Gon : UserRole.Collector;
            var user = new User(dto.Username, dto.Email, passwordHash, dto.PhoneNumber, role);

            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Novo usuário registrado: {Username} ({Email})", user.Username, user.Email);

            var token = _jwtService.GenerateToken(user);

            return (MapToAuthResponse(user), token);
        }

        public async Task<(AuthResponseDto dto, string token)> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email, ct);

            if (user is null || !_passwordHasher.Verify(dto.Password, user.PasswordHash))
                throw new InvalidCredentialsException();

            if (!user.IsActive)
                throw new DomainException("Conta desativada. Entre em contato com o suporte.");

            _logger.LogInformation("Login realizado: {Username}", user.Username);

            var token = _jwtService.GenerateToken(user);

            return (MapToAuthResponse(user), token);
        }

        public async Task<AuthResponseDto> GetMeAsync(Guid userId, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, ct)
                ?? throw new UserNotFoundException();

            return MapToAuthResponse(user);
        }

        public async Task ForgotPasswordAsync(ForgotPasswordDto dto, CancellationToken ct = default)
        {
            // Sempre retorna sem erro mesmo se email não existir — evita enumeração de usuários
            var user = await _userRepository.GetByEmailAsync(dto.Email, ct);
            if (user is null) return;

            var resetToken = new PasswordResetToken(user.Id);
            await _passwordResetTokenRepository.AddAsync(resetToken, ct);
            await _unitOfWork.CommitAsync(ct);

            await _emailService.SendPasswordResetEmailAsync(
                user.Email,
                user.Username,
                resetToken.Token,
                ct);

            _logger.LogInformation("Token de reset enviado para: {Email}", user.Email);
        }

        public async Task ResetPasswordAsync(ResetPasswordDto dto, CancellationToken ct = default)
        {
            var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(dto.Token, ct)
                ?? throw new DomainException("Token inválido.");

            if (!resetToken.IsValid)
                throw new DomainException("Token expirado ou já utilizado.");

            var user = await _userRepository.GetByIdAsync(resetToken.UserId, ct)
                ?? throw new UserNotFoundException();

            User.ValidatePasswordStrength(dto.NewPassword);

            if (_passwordHasher.Verify(dto.NewPassword, user.PasswordHash))
                throw new DomainException("A nova senha não pode ser igual à senha atual.");

            var newHash = _passwordHasher.Hash(dto.NewPassword);
            user.UpdatePasswordHash(newHash);

            resetToken.MarkAsUsed();

            _userRepository.Update(user);
            _passwordResetTokenRepository.Update(resetToken);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Senha redefinida para usuário: {UserId}", user.Id);
        }

        private static AuthResponseDto MapToAuthResponse(User user) =>
            new(user.Id, user.Username, user.Email, user.Role.ToString(), user.PhoneNumber, user.ProfilePicUrl);
    }
}
