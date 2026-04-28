using Kureimo.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Persistence.Config
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(u => u.Id);

            builder.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(30);

            builder.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(u => u.PasswordHash)
                .IsRequired();

            builder.Property(u => u.Role)
                .IsRequired()
                .HasConversion<string>(); // Salva como "Collector", "Gon", "Admin" no banco

            builder.Property(u => u.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(u => u.CreatedAt).IsRequired();
            builder.Property(u => u.UpdatedAt);

            // Índices únicos para email e username
            builder.HasIndex(u => u.Email).IsUnique();
            builder.HasIndex(u => u.Username).IsUnique();
        }
    }

    public class SetConfiguration : IEntityTypeConfiguration<Set>
    {
        public void Configure(EntityTypeBuilder<Set> builder)
        {
            builder.HasKey(s => s.Id);

            builder.Property(s => s.Title)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.Description)
                .HasMaxLength(500);

            builder.Property(s => s.AccessToken)
                .IsRequired()
                .HasMaxLength(12);

            builder.Property(s => s.GonId)
                .IsRequired();

            builder.Property(s => s.ClaimOpensAt)
                .IsRequired();

            builder.Property(s => s.Status)
                .IsRequired()
                .HasConversion<string>();

            builder.Property(s => s.ImageUrl)
                .IsRequired();

            builder.Property(s => s.CreatedAt).IsRequired();
            builder.Property(s => s.UpdatedAt);

            builder.Property(s => s.DeletedAt);

            builder.Property(s => s.BackgroundColor)
            .IsRequired()
            .HasMaxLength(7); // #FFFFFF

            builder.Property(s => s.FontColor)
                .IsRequired()
                .HasMaxLength(7);

            builder.Property(s => s.FontStyle)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(s => s.CancelledAt); // nullable

            // AccessToken precisa ser único — é o link público do set
            builder.HasIndex(s => s.AccessToken).IsUnique();

            // Um GON tem muitos sets
            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.GonId)
                .OnDelete(DeleteBehavior.Restrict);

            // Um set tem muitos photocards
            builder.HasMany(s => s.Photocards)
                .WithOne()
                .HasForeignKey(p => p.SetId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(s => s.DeletedAt == null && s.CancelledAt == null);

            builder.Navigation(s => s.Photocards)
                .HasField("_photocards")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class PhotocardConfiguration : IEntityTypeConfiguration<Photocard>
    {
        public void Configure(EntityTypeBuilder<Photocard> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p => p.ArtistName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.Version)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(p => p.CreatedAt).IsRequired();

            // xmin é uma coluna de sistema do PostgreSQL atualizada automaticamente
            // a cada UPDATE na linha — usamos ela como concurrency token.
            // Quando dois usuários tentam dar claim ao mesmo tempo,
            // o banco detecta que o xmin mudou e o segundo recebe conflito.
            // Não precisamos de nenhuma propriedade na entidade — o Npgsql cuida disso.
            builder.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate()
                .IsRowVersion();

            // Um photocard tem muitos claims
            builder.HasMany(p => p.Claims)
                .WithOne()
                .HasForeignKey(c => c.PhotocardId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(p => p.Claims)
                .HasField("_claims")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }

    public class ClaimConfiguration : IEntityTypeConfiguration<Claim>
    {
        public void Configure(EntityTypeBuilder<Claim> builder)
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.PhotocardId).IsRequired();
            builder.Property(c => c.UserId).IsRequired();

            builder.Property(c => c.ClaimedAt)
                .IsRequired();

            builder.Property(c => c.QueuePosition)
                .IsRequired();

            builder.Property(c => c.CreatedAt).IsRequired();

            // Um usuário não pode dar claim duas vezes no mesmo photocard
            builder.HasIndex(c => new { c.PhotocardId, c.UserId }).IsUnique();

            // Índice para buscar claims por photocard ordenados por posição
            builder.HasIndex(c => new { c.PhotocardId, c.QueuePosition });

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
