using Kureimo.Application.DTOs;
using Kureimo.Application.Interfaces;
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
        private readonly IPasswordHasher _passwordHasher;
        private readonly IStorageService _storageService;
        private readonly ILogger<UserService> _logger;

        public UserService(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IStorageService storageService,
            ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _storageService = storageService;
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

        public async Task UpdatePasswordAsync(Guid id, UpdatePasswordDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            if (id != requestingUserId)
                throw new UnauthorizedDomainException();

            var user = await _userRepository.GetByIdAsync(id, ct)
                ?? throw new UserNotFoundException();

            if (!_passwordHasher.Verify(dto.CurrentPassword, user.PasswordHash))
                throw new InvalidCredentialsException();

            var newHash = _passwordHasher.Hash(dto.NewPassword);
            user.UpdatePasswordHash(newHash);

            _userRepository.Update(user);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Senha atualizada: {UserId}", id);
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

            _logger.LogInformation("Conta desativada: {UserId}", id);
        }

        private static UserDto MapToDto(Domain.Entities.User user) =>
            new(user.Id, user.Username, user.Email, user.Role.ToString(), user.IsActive, user.PhoneNumber, user.ProfilePicUrl, user.CreatedAt);
    }
}
