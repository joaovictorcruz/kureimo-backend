using Kureimo.Domain.Exceptions;
using Kureimo.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kureimo.Infra.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public UnitOfWork(AppDbContext context)
        {
            _context = context;
        }

        public async Task CommitAsync(CancellationToken ct = default)
        {
            try
            {
                await _context.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Dois usuários tentaram modificar o mesmo registro simultaneamente.
                // Traduzimos para ConcurrencyException (do domínio) para que
                // o ClaimService possa fazer o retry sem conhecer EF Core.
                throw new ConcurrencyException();
            }
        }
    }

}
