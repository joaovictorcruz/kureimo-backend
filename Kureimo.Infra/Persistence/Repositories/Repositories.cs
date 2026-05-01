using Kureimo.Domain.Entities;
using Kureimo.Domain.Enums;
using Kureimo.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Persistence.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _context;

        public UserRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
            => await _context.Users.FirstOrDefaultAsync(u => u.Email == email.ToLower(), ct);

        public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
            => await _context.Users.FirstOrDefaultAsync(u => u.Username == username.ToLower(), ct);

        public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
            => await _context.Users.AnyAsync(u => u.Email == email.ToLower(), ct);

        public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
            => await _context.Users.AnyAsync(u => u.Username == username.ToLower(), ct);

        public async Task AddAsync(User user, CancellationToken ct = default)
            => await _context.Users.AddAsync(user, ct);

        public void Update(User user)
            => _context.Users.Update(user);
    }

    public class SetRepository : ISetRepository
    {
        private readonly AppDbContext _context;

        public SetRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Set?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.Sets.FirstOrDefaultAsync(s => s.Id == id, ct);

        public async Task<Set?> GetByAccessTokenAsync(string accessToken, CancellationToken ct = default)
            => await _context.Sets.FirstOrDefaultAsync(s => s.AccessToken == accessToken, ct);

        /// <summary>
        /// Carrega o set com todos os photocards e seus claims.
        /// Usado na página do set — snapshot inicial antes do SignalR assumir.
        /// </summary>
        public async Task<Set?> GetByAccessTokenWithDetailsAsync(string accessToken, CancellationToken ct = default)
            => await _context.Sets
                .Include(s => s.Photocards.OrderBy(p => p.Order))
                    .ThenInclude(p => p.Claims)
                .FirstOrDefaultAsync(s => s.AccessToken == accessToken, ct);

        public async Task<(IEnumerable<Set> Items, int TotalCount)> GetByGonIdAsync(Guid gonId, int page, int pageSize, CancellationToken ct = default)
        {
            var query = _context.Sets
                .Include(s => s.Photocards)
                .Where(s => s.GonId == gonId)
                .OrderByDescending(s => s.CreatedAt);

            var totalCount = await query.CountAsync(ct);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, totalCount);
        }

        public async Task AddAsync(Set set, CancellationToken ct = default)
            => await _context.Sets.AddAsync(set, ct);

        public async Task<IEnumerable<Set>> GetClosedByGonIdAsync(Guid gonId, CancellationToken ct = default)
            => await _context.Sets
                .Where(s => s.GonId == gonId && s.Status == SetStatus.Closed)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync(ct);

        public async Task SoftDeleteAllClosedByGonIdAsync(Guid gonId, CancellationToken ct = default)
        {
            var closedSets = await _context.Sets
                .Where(s => s.GonId == gonId && s.Status == SetStatus.Closed)
                .ToListAsync(ct);

            foreach (var set in closedSets)
                set.SoftDelete();
        }

        public async Task<IEnumerable<Set>> GetPublishedDueForOpenAsync(CancellationToken ct = default)
            => await _context.Sets
                .Where(s => s.Status == SetStatus.Published
                         && s.ClaimOpensAt <= DateTimeOffset.UtcNow)
                .ToListAsync(ct);

        public void Update(Set set)
            => _context.Sets.Update(set);
    }

    public class PhotocardRepository : IPhotocardRepository
    {
        private readonly AppDbContext _context;

        public PhotocardRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Photocard?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => await _context.Photocards.FirstOrDefaultAsync(p => p.Id == id, ct);

        /// <summary>
        /// Carrega o photocard com os claims existentes e o RowVersion atual.
        /// Essencial para o mecanismo de concorrência otimista do claim.
        /// </summary>
        public async Task<Photocard?> GetByIdWithClaimsAsync(Guid id, CancellationToken ct = default)
            => await _context.Photocards
                .Include(p => p.Claims)
                .FirstOrDefaultAsync(p => p.Id == id, ct);

        public async Task AddAsync(Photocard photocard, CancellationToken ct = default)
            => await _context.Photocards.AddAsync(photocard, ct);

        public void Update(Photocard photocard)
            => _context.Photocards.Update(photocard);

        public void Remove(Photocard photocard)
            => _context.Photocards.Remove(photocard);
    }

    public class ClaimRepository : IClaimRepository
    {
        private readonly AppDbContext _context;

        public ClaimRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Claim>> GetByPhotocardIdAsync(Guid photocardId, CancellationToken ct = default)
            => await _context.Claims
                .Where(c => c.PhotocardId == photocardId)
                .OrderBy(c => c.QueuePosition)
                .ToListAsync(ct);

        public async Task<Claim?> GetByUserAndPhotocardAsync(Guid userId, Guid photocardId, CancellationToken ct = default)
            => await _context.Claims
                .FirstOrDefaultAsync(c => c.UserId == userId && c.PhotocardId == photocardId, ct);

        public async Task AddAsync(Claim claim, CancellationToken ct = default)
            => await _context.Claims.AddAsync(claim, ct);
    }
}
