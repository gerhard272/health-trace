using HealthTrace.DAL.Data;
using HealthTrace.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace HealthTrace.DAL.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : class
    {
        private readonly HealthTraceDbContext _context;
        private readonly DbSet<T> _dbSet;

        public GenericRepository(HealthTraceDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();

            // Ensures that every entity T passed to the repository
            // has a DbSet registered in HealthTraceDbContext,
            // otherwise it throws.
            EnsureDbSetIsValid();
        }

        public async Task<T?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            return await _dbSet.FindAsync(id, cancellationToken);
        }

        public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await _dbSet
                .Where(predicate)
                .AsNoTracking()
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            await _dbSet.AddAsync(entity, cancellationToken);
        }

        public void Update(T entity)
        {
            _dbSet.Update(entity);
        }

        public void Delete(T entity)
        {
            _dbSet.Remove(entity);
        }

        private void EnsureDbSetIsValid()
        {
            if (_dbSet == null)
            {
                throw new InvalidOperationException($"DbSet for {typeof(T).Name} is not configured!");
            }
        }
    }
}