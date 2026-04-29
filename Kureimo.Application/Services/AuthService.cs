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
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtService jwtService,
            ILogger<AuthService> logger)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default)
        {
            // Verifica unicidade antes de criar a entidade
            // (regra de unicidade é de infra, não do domínio puro)
            if (await _userRepository.EmailExistsAsync(dto.Email, ct))
                throw new EmailAlreadyInUseException();

            if (await _userRepository.UsernameExistsAsync(dto.Username, ct))
                throw new UsernameAlreadyInUseException();

            var passwordHash = _passwordHasher.Hash(dto.Password);
            var role = dto.IsGon ? UserRole.Gon : UserRole.Collector;
            var user = new User(dto.Username, dto.Email, passwordHash, dto.PhoneNumber, role);

            await _userRepository.AddAsync(user, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Novo usuário registrado: {Username} ({Email})", user.Username, user.Email);

            var token = _jwtService.GenerateToken(user);

            return MapToAuthResponse(user, token);
        }

        public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByEmailAsync(dto.Email, ct);

            if (user is null || !_passwordHasher.Verify(dto.Password, user.PasswordHash))
                throw new InvalidCredentialsException();

            if (!user.IsActive)
                throw new DomainException("Conta desativada. Entre em contato com o suporte.");

            _logger.LogInformation("Login realizado: {Username}", user.Username);

            var token = _jwtService.GenerateToken(user);

            return MapToAuthResponse(user, token);
        }

        private static AuthResponseDto MapToAuthResponse(User user, string token) =>
            new(token, user.Username, user.Email, user.Role.ToString(), user.PhoneNumber, user.ProfilePicUrl);
    }
}
