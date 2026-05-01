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
        private readonly IRealtimeNotificationService _notificationService;
        private readonly IStorageService _storageService;
        private readonly ILogger<SetService> _logger;

        public SetService(
            ISetRepository setRepository,
            IUserRepository userRepository,
            IPhotocardRepository photocardRepository,
            IUnitOfWork unitOfWork,
            IRealtimeNotificationService notificationService,
            IStorageService storageService,
            ILogger<SetService> logger)
        {
            _setRepository = setRepository;
            _userRepository = userRepository;
            _photocardRepository = photocardRepository;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _storageService = storageService;
            _logger = logger;
        }

        public async Task<SetDto> CreateAsync(CreateSetDto dto, Stream imageStream, string imageFileName, Guid gonId, CancellationToken ct = default)
        {
            var gon = await _userRepository.GetByIdAsync(gonId, ct)
                ?? throw new UserNotFoundException();

            if (gon.Role != UserRole.Gon && gon.Role != UserRole.Admin)
                throw new UnauthorizedDomainException();

            var tempToken = Guid.NewGuid().ToString("N")[..12];

            var imageUrl = await _storageService.UploadSetImageAsync(imageStream, imageFileName, tempToken, ct);

            // O domínio valida o título e o horário internamente
            var set = new Set(dto.Title, gonId, imageUrl, dto.BackgroundColor, dto.FontColor, dto.FontStyle, dto.ClaimOpensAt, dto.Description);

            await _setRepository.AddAsync(set, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set criado: {SetId} por GON {GonId}", set.Id, gonId);

            return MapToDto(set);
        }

        public async Task<SetDetailDto> GetByAccessTokenAsync(string accessToken, Guid requestingUserId, string requestingUserRole, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenWithDetailsAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            // Sets em Draft não são acessíveis via link ainda
            if (set.Status == SetStatus.Draft)
            {
                var isOwner = set.GonId == requestingUserId;
                var isPrivileged = requestingUserRole == "Gon" || requestingUserRole == "Admin";

                if (!isPrivileged || !isOwner)
                    throw new SetNotFoundException(accessToken);
            }

            var gon = await _userRepository.GetByIdAsync(set.GonId, ct)
                 ?? throw new UserNotFoundException();

            return MapToDetailDto(set, gon);
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

        public async Task<PhotocardDto> UpdatePhotocardAsync(string accessToken, Guid photocardId, UpdatePhotocardDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenWithDetailsAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            var photocard = set.Photocards.FirstOrDefault(p => p.Id == photocardId)
                ?? throw new PhotocardNotFoundException(photocardId);

            photocard.Update(dto.ArtistName, dto.Version);

            _photocardRepository.Update(photocard);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Photocard atualizado: {PhotocardId}", photocardId);

            return MapToPhotocardDto(photocard);
        }

        public async Task RemovePhotocardAsync(string accessToken, Guid photocardId, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenWithDetailsAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            var photocard = set.Photocards.FirstOrDefault(p => p.Id == photocardId)
                ?? throw new PhotocardNotFoundException(photocardId);

            set.RemovePhotocard(photocardId);

            _photocardRepository.Remove(photocard);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Photocard removido: {PhotocardId} do Set {SetId}", photocardId, set.Id);
        }

        public async Task ReorderPhotocardsAsync(string accessToken, ReorderPhotocardsDto dto, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenWithDetailsAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            set.ReorderPhotocards(dto.OrderedIds);

            // Persiste a nova ordem de cada photocard
            foreach (var photocard in set.Photocards)
                _photocardRepository.Update(photocard);

            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Photocards reordenados no Set {SetId}", set.Id);
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

        public async Task CancelAsync(string accessToken, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            set.Cancel();

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Set cancelado: {SetId} por GON {GonId}", set.Id, requestingUserId);
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

            if (dto.BackgroundColor is not null)
                set.UpdateBackgroundColor(dto.BackgroundColor);

            if (dto.FontColor is not null)
                set.UpdateFontColor(dto.FontColor);

            if (dto.FontStyle is not null)
                set.UpdateFontStyle(dto.FontStyle);


            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);
        }

        public async Task<SetDto> UpdateSetImageAsync(string accessToken, Stream imageStream, string fileName, Guid requestingUserId, CancellationToken ct = default)
        {
            var set = await _setRepository.GetByAccessTokenAsync(accessToken, ct)
                ?? throw new SetNotFoundException(accessToken);

            EnsureIsOwner(set, requestingUserId);

            var url = await _storageService.UploadSetImageAsync(imageStream, fileName, accessToken, ct);

            set.UpdateImageUrl(url);

            _setRepository.Update(set);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation("Imagem do set atualizada: {SetId}", set.Id);

            return MapToDto(set);
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

        /// <summary>
        /// Chamado pelo Worker via endpoint interno após persistir o Open no banco.
        /// Responsabilidade exclusiva: disparar o SignalR para os collectors.
        /// </summary>
        public async Task NotifySetOpenedAsync(string accessToken, CancellationToken ct = default)
        {
            await _notificationService.NotifySetStatusChangedAsync(accessToken, "Open", ct);

            _logger.LogInformation("Notificação SignalR disparada para set {AccessToken}", accessToken);
        }

        private static SetDto MapToDto(Set set) =>
            new(set.Id,
                set.Title,
                set.Description,
                set.AccessToken,
                set.Status.ToString(),
                set.ImageUrl,
                set.BackgroundColor,
                set.FontColor,
                set.FontStyle,
                set.ClaimOpensAt,
                set.Photocards.Count,
                set.CreatedAt);

        private static SetDetailDto MapToDetailDto(Set set, User gon) =>
            new(set.Id,
                set.Title,
                set.Description,
                set.AccessToken,
                set.Status.ToString(),
                set.ImageUrl,
                set.BackgroundColor,
                set.FontColor,
                set.FontStyle,
                set.ClaimOpensAt,
                new GonInfoDto(gon.Id, gon.Username, gon.ProfilePicUrl),
                set.Photocards.Select(MapToPhotocardDetailDto));

        private static PhotocardDto MapToPhotocardDto(Photocard pc) =>
            new(pc.Id, pc.ArtistName, pc.Version, pc.Order, pc.TotalClaims);

        private static PhotocardDetailDto MapToPhotocardDetailDto(Photocard pc) =>
            new(pc.Id,
                pc.ArtistName,
                pc.Version,
                pc.Order,
                pc.Claims.Select(c => new ClaimDto(
                    c.Id,
                    c.PhotocardId,
                    c.UserId,
                    string.Empty, // username será preenchido se necessário
                    null,
                    null,
                    c.ClaimedAt,
                    c.QueuePosition)));
    }
}
