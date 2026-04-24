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
    public class SetService
    {
        private readonly ISetRepository _setRepository;
        private readonly IPhotocardRepository _photocardRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SetService> _logger;

        public SetService(
            ISetRepository setRepository,
            IUserRepository userRepository,
            IPhotocardRepository photocardRepository,
            IUnitOfWork unitOfWork,
            ILogger<SetService> logger)
        {
            _setRepository = setRepository;
            _userRepository = userRepository;
            _photocardRepository = photocardRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<SetDto> CreateAsync(CreateSetDto dto, Guid gonId, CancellationToken ct = default)
        {
            var gon = await _userRepository.GetByIdAsync(gonId, ct)
                ?? throw new UserNotFoundException();

            if (gon.Role != UserRole.Gon && gon.Role != UserRole.Admin)
                throw new UnauthorizedDomainException();

            // O domínio valida o título e o horário internamente
            var set = new Set(dto.Title, gonId, dto.ClaimOpensAt, dto.Description);

            await _setRepository.AddAsync(set, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set criado: {SetId} por GON {GonId}", set.Id, gonId);

            return MapToDto(set);
        }

        public async Task<SetDetailDto> GetByAccessTokenAsync(string accessToken, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenWithDetailsAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            // Sets em Draft não são acessíveis via link ainda
            if (set.Status == SetStatus.Draft)
                throw new SetNotFoundException(accessToken);

            return MapToDetailDto(set);
        }

        public async Task<IEnumerable<SetDto>> GetMySetssAsync(Guid gonId, CancellationToken ct = default)
        {
            var sets = await _setRepository.GetByGonIdAsync(gonId, ct);
            return sets.Select(MapToDto);
        }

        public async Task<PhotocardDto> AddPhotocardAsync(string accessToken, AddPhotocardDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByIdAsync(
                await ResolveSetIdFromToken(accessToken, ct), ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            var photocard = set.AddPhotocard(dto.ArtistName, dto.Version, dto.ImageUrl);

            await _photocardRepository.AddAsync(photocard, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Photocard adicionado: {PhotocardId} ao Set {SetId}",
                photocard.Id, set.Id);

            return MapToPhotocardDto(photocard);
        }

        public async Task PublishAsync(string accessToken, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenWithDetailsAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            set.Publish();

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set publicado: {SetId}", set.Id);
        }

        public async Task OpenAsync(string accessToken, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            set.Open();

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set aberto para claims: {SetId}", set.Id);
        }

        public async Task CloseAsync(string accessToken, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            set.Close();

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set encerrado: {SetId}", set.Id);
        }

        public async Task UpdateAsync(string accessToken, UpdateSetDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            if (dto.Title is not null)
                set.UpdateTitle(dto.Title);

            if (dto.ClaimOpensAt.HasValue)
                set.UpdateClaimOpensAt(dto.ClaimOpensAt.Value);

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);
        }

        // Garante que quem está operando é o dono do set
        private static void EnsureIsOwner(Set set, Guid requestingUserId)
        {
            if (set.GonId != requestingUserId)
                throw new UnauthorizedDomainException();
        }

        // Resolve o SetId a partir do AccessToken sem carregar os photocards
        private async Task<Guid> ResolveSetIdFromToken(string accessToken, CancellationToken ct)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);
            return set.Id;
        }

        private static SetDto MapToDto(Set set) =>
            new(set.Id,
                set.Title,
                set.Description,
                set.AccessToken,
                set.Status.ToString(),
                set.ClaimOpensAt,
                set.Photocards.Count,
                set.CreatedAt);

        private static SetDetailDto MapToDetailDto(Set set) =>
            new(set.Id,
                set.Title,
                set.Description,
                set.AccessToken,
                set.Status.ToString(),
                set.ClaimOpensAt,
                set.Photocards.Select(MapToPhotocardDetailDto));

        private static PhotocardDto MapToPhotocardDto(Photocard pc) =>
            new(pc.Id, pc.ArtistName, pc.Version, pc.ImageUrl, pc.TotalClaims);

        private static PhotocardDetailDto MapToPhotocardDetailDto(Photocard pc) =>
            new(pc.Id,
                pc.ArtistName,
                pc.Version,
                pc.ImageUrl,
                pc.Claims.Select(c => new ClaimDto(
                    c.Id,
                    c.PhotocardId,
                    c.UserId,
                    string.Empty, // username será preenchido se necessário
                    c.ClaimedAt,
                    c.QueuePosition)));
    }
}
