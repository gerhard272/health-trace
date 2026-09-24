using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HealthTrace.DAL.Data
{
    public class HealthTraceDbContextFactory : IDesignTimeDbContextFactory<HealthTraceDbContext>
    {
        public HealthTraceDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets("ee264c38-c996-43d2-a298-3dab2a0fe59a")
                .Build();

            var connectionString = configuration.GetConnectionString("HealthTraceDb");

            var optionsBuilder = new DbContextOptionsBuilder<HealthTraceDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new HealthTraceDbContext(optionsBuilder.Options, new DesignTimeCurrentUserService());
        }

        private class DesignTimeCurrentUserService : ICurrentUserService
        {
            public int? UserId => null;
        }
    }
}