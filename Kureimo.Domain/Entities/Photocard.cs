using Kureimo.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Entities
{
    public class Photocard : BaseEntity
    {
        public Guid SetId { get; private set; }
        public string ArtistName { get; private set; }

        public string Version { get; private set; }

        public string ImageUrl { get; private set; }

        private readonly List<Claim> _claims = new();
        public IReadOnlyCollection<Claim> Claims => _claims.AsReadOnly();

        /// <summary>
        /// Controle de concorrência otimista — EF Core usa isso para detectar conflito
        /// quando dois usuários tentam dar claim ao mesmo tempo.
        /// </summary>
        public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

        // EF Core constructor
        private Photocard() { }

        internal Photocard(Guid setId, string artistName, string version, string imageUrl)
        {
            ValidateArtistName(artistName);
            ValidateVersion(version);
            ValidateImageUrl(imageUrl);

            SetId = setId;
            ArtistName = artistName.Trim();
            Version = version.Trim();
            ImageUrl = imageUrl.Trim();
        }

        /// <summary>
        /// Registra um claim neste photocard.
        /// A responsabilidade de validar se o set está aberto é do SetService,
        /// mas a regra de "já foi claimed" pertence ao domínio.
        /// </summary>
        internal Claim RegisterClaim(Guid userId, DateTimeOffset serverTimestamp)
        {
            var position = _claims.Count + 1;
            var claim = new Claim(Id, userId, serverTimestamp, position);
            _claims.Add(claim);

            return claim;
        }

        public bool HasBeenClaimedBy(Guid userId)
        {
            return _claims.Any(c => c.UserId == userId);
        }

        public int TotalClaims => _claims.Count;

        private static void ValidateArtistName(string artistName)
        {
            if (string.IsNullOrWhiteSpace(artistName))
                throw new DomainException("O nome do artista não pode ser vazio.");

            if (artistName.Trim().Length > 100)
                throw new DomainException("O nome do artista deve ter no máximo 100 caracteres.");
        }

        private static void ValidateVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new DomainException("A versão do photocard não pode ser vazia.");

            if (version.Trim().Length > 100)
                throw new DomainException("A versão deve ter no máximo 100 caracteres.");
        }

        private static void ValidateImageUrl(string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
                throw new DomainException("A URL da imagem não pode ser vazia.");
        }
    }
}
