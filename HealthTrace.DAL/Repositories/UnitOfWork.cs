using System.Collections.Concurrent;
using HealthTrace.DAL.Data;
using HealthTrace.DAL.Repositories.Interfaces;


namespace HealthTrace.DAL.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly HealthTraceDbContext _context;
        private readonly ConcurrentDictionary<Type, object> _repositories = new();

        //dependency injection del DbContext tramite il costruttore
        public UnitOfWork(HealthTraceDbContext context)
        {
            _context = context;
            _repositories = new ConcurrentDictionary<Type, object>();
        }

        public IGenericRepository<T> Repository<T>() where T : class
        {
            var type = typeof(T);

            if (!_repositories.ContainsKey(typeof(T)))
            {
                var repositoryInstance = new GenericRepository<T>(_context);
                _repositories[type] = repositoryInstance;
            }
            return (IGenericRepository<T>)_repositories[typeof(T)];
        }
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        // Implementazione di Dispose() per liberare le risorse del DbContext
        public void Dispose()
        {
            _context.Dispose();
        }
    }
}