using Kureimo.Domain.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Domain.Entities
{
    public class Review : BaseEntity
    {
        public Guid TargetUserId { get; private set; }
        public Guid AuthorUserId { get; private set; }
        public int Rating { get; private set; }
        public string Comment { get; private set; } = string.Empty;

        private Review() { }

        public Review(Guid targetUserId, Guid authorUserId, int rating, string comment)
        {
            if (targetUserId == authorUserId)
                throw new DomainException("Você não pode avaliar a si mesmo.");

            ValidateRating(rating);
            ValidateComment(comment);

            TargetUserId = targetUserId;
            AuthorUserId = authorUserId;
            Rating = rating;
            Comment = comment.Trim();
        }

        /// <summary>
        /// Um autor só pode ter uma avaliação por alvo — reenviar edita a existente
        /// em vez de criar duplicata.
        /// </summary>
        public void Update(int rating, string comment)
        {
            ValidateRating(rating);
            ValidateComment(comment);

            Rating = rating;
            Comment = comment.Trim();
            SetUpdatedAt();
        }

        private static void ValidateRating(int rating)
        {
            if (rating < 1 || rating > 5)
                throw new DomainException("A avaliação deve ser entre 1 e 5 estrelas.");
        }

        private static void ValidateComment(string comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
                throw new DomainException("O comentário não pode ser vazio.");

            if (comment.Trim().Length > 500)
                throw new DomainException("O comentário deve ter no máximo 500 caracteres.");
        }
    }
}
