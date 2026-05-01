using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Kureimo.Domain.Exceptions;
using Kureimo.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Storage
{
    public class CloudinaryService : IStorageService
    {
        private readonly Cloudinary _cloudinary;

        // Tipos permitidos
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        // 5MB
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public CloudinaryService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"]
                ?? throw new InvalidOperationException("Cloudinary:CloudName não configurado.");
            var apiKey = configuration["Cloudinary:ApiKey"]
                ?? throw new InvalidOperationException("Cloudinary:ApiKey não configurado.");
            var apiSecret = configuration["Cloudinary:ApiSecret"]
                ?? throw new InvalidOperationException("Cloudinary:ApiSecret não configurado.");

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
        }

        public async Task<string> UploadProfilePicAsync(
            Stream imageStream,
            string fileName,
            Guid userId,
            CancellationToken ct = default)
        {
            ValidateFile(fileName, imageStream.Length);

            var uploadParams = new ImageUploadParams
            {
                // public_id único por usuário — sobrescreve a foto anterior automaticamente
                PublicId = $"kureimo/profiles/{userId}",
                File = new FileDescription(fileName, imageStream),
                Transformation = new Transformation()
                    .Width(400).Height(400)
                    .Crop("fill")
                    .Gravity("face")     // centraliza no rosto automaticamente
                    .Quality("auto")
                    .FetchFormat("webp") // converte para WebP automaticamente
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);

            if (result.Error is not null)
                throw new DomainException($"Erro ao fazer upload da imagem: {result.Error.Message}");

            return result.SecureUrl.ToString();
        }

        public async Task<string> UploadSetImageAsync(
            Stream imageStream,
            string fileName,
            string accessToken,
            CancellationToken ct = default)
        {
            ValidateFile(fileName, imageStream.Length);

            var uploadParams = new ImageUploadParams
            {
                // public_id único por set — sobrescreve a imagem anterior automaticamente
                PublicId = $"kureimo/sets/{accessToken}",
                File = new FileDescription(fileName, imageStream),
                Transformation = new Transformation()
                    .Width(1200).Height(630)
                    .Crop("fill")
                    .Quality("auto")
                    .FetchFormat("webp")
            };

            var result = await _cloudinary.UploadAsync(uploadParams, ct);

            if (result.Error is not null)
                throw new DomainException($"Erro ao fazer upload da imagem: {result.Error.Message}");

            return result.SecureUrl.ToString();
        }

        private static void ValidateFile(string fileName, long sizeBytes)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(ext))
                throw new DomainException($"Formato inválido. Permitidos: {string.Join(", ", AllowedExtensions)}.");

            if (sizeBytes > MaxFileSizeBytes)
                throw new DomainException("A imagem deve ter no máximo 5MB.");
        }
    }
}
