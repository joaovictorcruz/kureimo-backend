using Kureimo.Application.DTOs;
using Kureimo.Domain.Entities;
using Kureimo.Domain.Enums;
using Kureimo.Domain.Exceptions;
using Kureimo.Domain.Interfaces;
using Kureimo.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Application.Services
{
    public class UserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IStorageService _storageService;
        private readonly ILogtoManagementService _logtoManagementService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IStorageService storageService,
            ILogtoManagementService logtoManagementService,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _storageService = storageService;
            _logtoManagementService = logtoManagementService;
            _logger = logger;
        }

        public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new UserNotFoundException();   

            return MapToDto(user);
        }

        public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            if (id != requestingUserId)
                throw new UnauthorizedDomainException();

            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new UserNotFoundException();

            if (dto.Username is not null)
            {
                if (!string.Equals(dto.Username.Trim().ToLower(), user.Username, StringComparison.Ordinal)
                    && await _userRepository.UsernameExistsAsync(dto.Username, ct))
                    throw new UsernameAlreadyInUseException();


                user.UpdateUsername(dto.Username);
            }

            if (dto.Email is not null)
            {
                if (!string.Equals(dto.Email.Trim().ToLower(), user.Email, StringComparison.Ordinal)
                    && await _userRepository.EmailExistsAsync(dto.Email, ct))
                    throw new EmailAlreadyInUseException();


                user.UpdateEmail(dto.Email);
            }

            if (dto.PhoneNumber is not null)
            {
                if (!string.Equals(dto.PhoneNumber.Trim().ToLower(), user.PhoneNumber, StringComparison.Ordinal)
                    && await _userRepository.EmailExistsAsync(dto.PhoneNumber, ct))
                    throw new EmailAlreadyInUseException();


                user.UpdatePhoneNumber(dto.PhoneNumber);
            }

            _userRepository.Update(user);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Perfil atualizado: {UserId}", id);

            return MapToDto(user);
        }

        public async Task<UserDto> UpdateProfilePicAsync(Guid id, Stream imageStream, string fileName, Guid requestingUserId, CancellationToken ct = default)
        {
            if (id != requestingUserId)
                throw new UnauthorizedDomainException();

            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new UserNotFoundException();

            var url = await _storageService.UploadProfilePicAsync(imageStream, fileName, id, ct);

            user.UpdateProfilePicUrl(url);

            _userRepository.Update(user);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Foto de perfil atualizada: {UserId}", id);

            return MapToDto(user);
        }

        public async Task PromoteToGonAsync(Guid targetUserId, Guid requestingUserId, CancellationToken ct = default)
        {
            var requestingUser = await _userRepository.GetByIdAsync(requestingUserId, ct)
                ?? throw new UserNotFoundException();

            // Apenas Admin pode promover
            if (requestingUser.Role != Domain.Enums.UserRole.Admin)
                throw new UnauthorizedDomainException();

            var targetUser = await _userRepository.GetByIdAsync(targetUserId, ct)
                ?? throw new UserNotFoundException();

            targetUser.PromoteToGon();

            _userRepository.Update(targetUser);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Usuário {TargetId} promovido a GON por {AdminId}", targetUserId, requestingUserId);
        }

        public async Task DeactivateAsync(Guid id, Guid requestingUserId, CancellationToken ct = default)
        {
            if (id != requestingUserId)
                throw new UnauthorizedDomainException();

            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new UserNotFoundException();

            user.Deactivate();

            _userRepository.Update(user);
            await _unitOfWork.CommitAsync(ct);

            await _logtoManagementService.DeleteUserAsync(user.LogtoId, ct);

            _logger.LogInformation("Conta desativada: {UserId}", id);
        }

        public async Task<UserDto> CompleteOnboardingAsync(Guid id, string email, string roleString, CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new UserNotFoundException();

            var normalizedEmail = email.Trim().ToLower();

            if (!string.Equals(normalizedEmail, user.Email, StringComparison.Ordinal)
                && await _userRepository.EmailExistsAsync(normalizedEmail, ct))
                throw new EmailAlreadyInUseException();

            if (!Enum.TryParse<UserRole>(roleString, ignoreCase: true, out var role) || role == UserRole.Admin)
                throw new DomainException("Role inválida. Use 'Gon' ou 'Collector'.");

            user.CompleteOnboarding(normalizedEmail, role);

            _userRepository.Update(user);
            await _unitOfWork.CommitAsync(ct);

            await _logtoManagementService.SetPrimaryEmailAsync(user.LogtoId, user.Email, ct);

            _logger.LogInformation("Onboarding completo: {UserId}, role {Role}", id, role);

            return MapToDto(user);
        }

        private static UserDto MapToDto(User user) =>
            new(user.Id, user.Username, user.Email, user.Role.ToString(), user.IsActive,
                user.PhoneNumber, user.ProfilePicUrl, user.ProfileCompleted, user.CreatedAt);
    }
}
