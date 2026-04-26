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
            var set = new Set(dto.Title, gonId, dto.ImageUrl, dto.ClaimOpensAt, dto.Description);

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

        public async Task<PagedResultDto<SetDto>> GetMySetsAsync(
            Guid gonId,
            PaginationDto pagination,
            CancellationToken ct = default)
        {
            var pageSize = Math.Clamp(pagination.PageSize, 1, 50); 
            var page = Math.Max(pagination.Page, 1);

            var (items, totalCount) = await _setRepository.GetByGonIdAsync(gonId, page, pageSize, ct);

            return new PagedResultDto<SetDto>(
                Items: items.Select(MapToDto),
                Page: page,
                PageSize: pageSize,
                TotalCount: totalCount,
                TotalPages: (int)Math.Ceiling(totalCount / (double)pageSize)
            );
        }

        public async Task<PhotocardDto> AddPhotocardAsync(string accessToken, AddPhotocardDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByIdAsync(
                await ResolveSetIdFromToken(accessToken, ct), ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            var photocard = set.AddPhotocard(dto.ArtistName, dto.Version);

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

            if (dto.ImageUrl is not null)
                set.UpdateImageUrl(dto.ImageUrl);

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);
        }

        public async Task SoftDeleteAsync(string accessToken, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            set.SoftDelete(); // domínio garante que só Closed pode ser deletado

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set removido do histórico: {SetId} por GOM {GonId}", set.Id, requestingUserId);
        }

        public async Task SoftDeleteAllClosedAsync(Guid gonId, CancellationToken ct = default)
        {
            await _setRepository.SoftDeleteAllClosedByGonIdAsync(gonId, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Histórico de sets fechados limpo para GOM {GonId}", gonId);
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
                set.ImageUrl,
                set.ClaimOpensAt,
                set.Photocards.Count,
                set.CreatedAt);

        private static SetDetailDto MapToDetailDto(Set set) =>
            new(set.Id,
                set.Title,
                set.Description,
                set.AccessToken,
                set.Status.ToString(),
                set.ImageUrl,
                set.ClaimOpensAt,
                set.Photocards.Select(MapToPhotocardDetailDto));

        private static PhotocardDto MapToPhotocardDto(Photocard pc) =>
            new(pc.Id, pc.ArtistName, pc.Version, pc.TotalClaims);

        private static PhotocardDetailDto MapToPhotocardDetailDto(Photocard pc) =>
            new(pc.Id,
                pc.ArtistName,
                pc.Version,
                pc.Claims.Select(c => new ClaimDto(
                    c.Id,
                    c.PhotocardId,
                    c.UserId,
                    string.Empty, // username será preenchido se necessário
                    c.ClaimedAt,
                    c.QueuePosition)));
    }
}
