using Kureimo.Domain.Enums;
using Kureimo.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Entities
{
    public class Set : BaseEntity
    {
        public string Title { get; private set; }
        public string? Description { get; private set; }
        public string ImageUrl { get; private set; }
        public Guid GonId { get; private set; }

        /// <summary>
        /// Token único que compõe o link compartilhado pelo GON.
        /// Ex: kureimo.com/set/{AccessToken}
        /// </summary>
        public string AccessToken { get; private set; }

        /// <summary>
        /// Momento exato (UTC) em que os claims serão abertos.
        /// </summary>
        public DateTimeOffset ClaimOpensAt { get; private set; }

        public SetStatus Status { get; private set; }

        private readonly List<Photocard> _photocards = new();
        public DateTimeOffset? DeletedAt { get; private set; }
        public IReadOnlyCollection<Photocard> Photocards => _photocards.AsReadOnly();
        private const int MinutesBeforeClaim = 10;

        private Set() { }

        public Set(string title, Guid gonId, string imageUrl, DateTimeOffset claimOpensAt, string? description = null)
        {
            ValidateTitle(title);
            ValidateClaimOpensAt(claimOpensAt);

            Title = title.Trim();
            Description = description?.Trim();
            ImageUrl = imageUrl.Trim();
            GonId = gonId;
            ClaimOpensAt = claimOpensAt;
            AccessToken = GenerateAccessToken();
            Status = SetStatus.Draft;
        }

        public Photocard AddPhotocard(string artistName, string version)
        {
            if (Status == SetStatus.Closed)
                throw new DomainException("Não é possível adicionar photocards a um set encerrado.");

            var photocard = new Photocard(Id, artistName, version);
            _photocards.Add(photocard);
            SetUpdatedAt();

            return photocard;
        }

        public void UpdateImageUrl(string imageUrl)
        {
            ValidateImageUrl(imageUrl);
            ImageUrl = imageUrl.Trim();
            SetUpdatedAt();
        }

        public void Publish()
        {
            if (Status != SetStatus.Draft)
                throw new DomainException("Apenas sets em Draft podem ser publicados.");

            if (!_photocards.Any())
                throw new DomainException("O set precisa ter ao menos um photocard para ser publicado.");

            if (ClaimOpensAt <= DateTimeOffset.UtcNow)
                ClaimOpensAt = DateTimeOffset.UtcNow.AddMinutes(15);

            Status = SetStatus.Published;
            SetUpdatedAt();
        }

        public void Open()
        {
            if (Status != SetStatus.Published)
                throw new DomainException("Apenas sets publicados podem ser abertos.");

            Status = SetStatus.Open;
            SetUpdatedAt();
        }

        public void Close()
        {
            if (Status == SetStatus.Closed)
                throw new DomainException("Set já está encerrado.");

            Status = SetStatus.Closed;
            SetUpdatedAt();
        }

        public void UpdateClaimOpensAt(DateTimeOffset claimOpensAt)
        {
            if (Status == SetStatus.Open || Status == SetStatus.Closed)
                throw new DomainException("Não é possível alterar o horário de um set já aberto ou encerrado.");

            if ((claimOpensAt - DateTimeOffset.UtcNow).TotalMinutes < MinutesBeforeClaim)
                throw new DomainException($"O horário de claim deve ser pelo menos {MinutesBeforeClaim} minutos no futuro.");

            ValidateClaimOpensAt(claimOpensAt);
            ClaimOpensAt = claimOpensAt;
            SetUpdatedAt();
        }

        public void UpdateTitle(string title)
        {
            ValidateTitle(title);
            Title = title.Trim();
            SetUpdatedAt();
        }

        public void SoftDelete()
        {
            if (Status != SetStatus.Closed)
                throw new DomainException("Apenas sets encerrados podem ser removidos do histórico.");

            if (DeletedAt.HasValue)
                throw new DomainException("Este set já foi removido.");

            DeletedAt = DateTimeOffset.UtcNow;
            SetUpdatedAt();
        }

        public bool IsClaimWindowActive()
        {
            var now = DateTimeOffset.UtcNow;
            var windowStart = ClaimOpensAt.AddMinutes(-10);
            var windowEnd = ClaimOpensAt.AddMinutes(10);
            return now >= windowStart && now <= windowEnd;
        }

        public bool IsClaimOpen()
        {
            return Status == SetStatus.Open;
        }

        private static void ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new DomainException("O título do set não pode ser vazio.");

            if (title.Trim().Length > 100)
                throw new DomainException("O título do set deve ter no máximo 100 caracteres.");
        }

        private static void ValidateClaimOpensAt(DateTimeOffset claimOpensAt)
        {
            if (claimOpensAt <= DateTimeOffset.UtcNow)
                throw new DomainException("O horário de abertura deve ser no futuro.");
        }

        private static string GenerateAccessToken()
        {
            // Token URL-safe de 12 caracteres — difícil de adivinhar, fácil de compartilhar
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("/", "_")
                .Replace("+", "-")
                .Replace("=", "")
                [..12];
        }

        private static void ValidateImageUrl(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new DomainException("A URL da imagem do set não pode ser vazia.");
        }
    }
}
