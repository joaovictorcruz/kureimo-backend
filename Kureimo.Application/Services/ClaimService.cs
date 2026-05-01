using Kureimo.Application.DTOs;
using Kureimo.Domain.Entities;
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
    public class ClaimService
    {
        private readonly ISetRepository _setRepository;
        private readonly IPhotocardRepository _photocardRepository;
        private readonly IUserRepository _userRepository;
        private readonly IClaimRepository _claimRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRealtimeNotificationService _notificationService;
        private readonly ILogger<ClaimService> _logger;

        // Máximo de tentativas em caso de conflito de concorrência
        private const int MaxRetryAttempts = 50;

        public ClaimService(
            ISetRepository setRepository,
            IPhotocardRepository photocardRepository,
            IUserRepository userRepository,
            IClaimRepository claimRepository,
            IUnitOfWork unitOfWork,
            IRealtimeNotificationService notificationService,
            ILogger<ClaimService> logger)
        {
            _setRepository = setRepository;
            _photocardRepository = photocardRepository;
            _userRepository = userRepository;
            _claimRepository = claimRepository;
            _unitOfWork = unitOfWork;
            _notificationService = notificationService;
            _logger = logger;
        }

        /// <summary>
        /// Registra o claim de um usuário em um photocard.
        ///
        /// O parâmetro serverTimestamp vem do RequestTimestampMiddleware na API —
        /// é o momento exato que a request chegou ao servidor, NUNCA do cliente.
        ///
        /// Em caso de dois usuários simultâneos, o mecanismo de concorrência otimista
        /// (RowVersion no Photocard) garante que apenas um persiste por vez.
        /// O segundo tenta novamente e entra na fila com a posição correta.
        /// </summary>
        public async Task<ClaimDto> RegisterClaimAsync(
            Guid photocardId,
            Guid userId,
            DateTimeOffset serverTimestamp,
            CancellationToken ct = default)
        {
            var user = await _userRepository.GetByIdAsync(userId, ct)
                ?? throw new UserNotFoundException();

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    return await AttemptClaimAsync(photocardId, userId, user.Username, serverTimestamp, ct);
                }
                catch (ConcurrencyException) when (attempt < MaxRetryAttempts)
                {
                    _logger.LogWarning(
                        "Conflito de concorrência no claim do photocard {PhotocardId}. Tentativa {Attempt}/{Max}",
                        photocardId, attempt, MaxRetryAttempts);
                }
            }

            throw new DomainException("Não foi possível registrar o claim após múltiplas tentativas. Tente novamente.");
        }

        public async Task<IEnumerable<ClaimDto>> GetClaimsByPhotocardAsync(Guid photocardId, CancellationToken ct = default)
        {
            var claims = await _photocardRepository.GetByIdWithClaimsAsync(photocardId, ct);

            if (claims is null)
                throw new PhotocardNotFoundException(photocardId);

            var userIds = claims.Claims.Select(c => c.UserId).Distinct().ToList();
            var usernames = new Dictionary<Guid, string>();
            var phoneNumbers = new Dictionary<Guid, string?>();
            var profilePics = new Dictionary<Guid, string?>();

            foreach (var uid in userIds)
            {
                var u = await _userRepository.GetByIdAsync(uid, ct);
                if (u is not null)
                    usernames[uid] = u.Username;
                    phoneNumbers[uid] = u.PhoneNumber;
                    profilePics[uid] = u.ProfilePicUrl;
            }

            return claims.Claims
                .OrderBy(c => c.QueuePosition)
                .Select(c => new ClaimDto(
                    c.Id,
                    c.PhotocardId,
                    c.UserId,
                    usernames.GetValueOrDefault(c.UserId, "unknown"),
                    phoneNumbers.GetValueOrDefault(c.UserId),
                    profilePics.GetValueOrDefault(c.UserId),
                    c.ClaimedAt,
                    c.QueuePosition));
        }


        private async Task<ClaimDto> AttemptClaimAsync(
            Guid photocardId,
            Guid userId,
            string username,
            DateTimeOffset serverTimestamp,
            CancellationToken ct)
        {
            var photocard = await _photocardRepository.GetByIdWithClaimsAsync(photocardId, ct)
                ?? throw new PhotocardNotFoundException(photocardId);

            // Carrega o set para verificar se o claim está aberto
            var set = await _setRepository.GetByIdAsync(photocard.SetId, ct)
                ?? throw new SetNotFoundException();

            var user = await _userRepository.GetByIdAsync(userId, ct)
                ?? throw new UserNotFoundException();

            if (!set.IsClaimOpen())
                throw new ClaimWindowNotOpenException();

            if (photocard.HasBeenClaimedBy(userId))
                throw new UserAlreadyClaimedException();

            // Domínio registra o claim com o timestamp do servidor
            var claim = photocard.RegisterClaim(userId, serverTimestamp);

            await _claimRepository.AddAsync(claim, ct);
            await _unitOfWork.CommitAsync(ct);

            _logger.LogInformation(
                "Claim registrado — Photocard: {PhotocardId} | Usuário: {UserId} | Posição: {Position} | Timestamp: {Timestamp}",
                photocardId, userId, claim.QueuePosition, claim.ClaimedAt);

            var claimDto = new ClaimDto(
                claim.Id,
                claim.PhotocardId,
                claim.UserId,
                username,
                user.PhoneNumber,
                user.ProfilePicUrl,
                claim.ClaimedAt,
                claim.QueuePosition);

            // Notifica todos os clientes conectados ao set via SignalR
            // Fire-and-forget intencional: não queremos que falha de notificação
            // impeça o claim de ser confirmado para o usuário
            _ = NotifyClaimAsync(set.AccessToken, claimDto);

            return claimDto;
        }

        private async Task NotifyClaimAsync(string accessToken, ClaimDto claimDto)
        {
            try
            {
                await _notificationService.NotifyClaimRegisteredAsync(accessToken, claimDto);
            }
            catch (Exception ex)
            {
                // Falha de notificação não é crítica — o claim já foi persistido
                _logger.LogError(ex, "Falha ao notificar claim via SignalR para o set {AccessToken}", accessToken);
            }
        }
    }
}
