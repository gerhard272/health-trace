using HealthTrace.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthTrace.DAL.Data
{
    public class HealthTraceDbContext : DbContext
    {
        private readonly ICurrentUserService _currentUserService;
        public HealthTraceDbContext(DbContextOptions<HealthTraceDbContext> options, 
                                    ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Symptom> Symptoms { get; set; }

        public DbSet<ExportRequest> ExportRequests { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(200);
                entity.Property(u => u.FirstName).IsRequired().HasMaxLength(50);
                entity.Property(u => u.LastName).IsRequired().HasMaxLength(50);
                entity.Property(u => u.CF).IsRequired().HasMaxLength(16);
                entity.Property(u => u.BirthPlace).HasMaxLength(50);
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.CF).IsUnique();
            });

            modelBuilder.Entity<Symptom>(entity =>
            {
                entity.Property(s => s.EventName).IsRequired().HasMaxLength(100);
                entity.Property(s => s.Description).HasMaxLength(500);
                entity.Property(s => s.EventDate).IsRequired();

                entity.HasIndex(s => s.UserId); //speeds up lookups by UserId

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Soft delete: deleted symptoms (IsDeleted = true) are automatically
                // excluded from every query, as for ExportRequest.
                entity.HasQueryFilter(s => !s.IsDeleted);
            });

            modelBuilder.Entity<ExportRequest>(entity =>
            {
                entity.Property(e => e.FileName).HasMaxLength(200);
                entity.Property(e => e.BlobName).HasMaxLength(300);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

                entity.HasIndex(e => e.UserId);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => !e.IsDeleted);
            });
        }

        public override int SaveChanges()
        {
            ApplyAuditInfo();
            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyAuditInfo();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditInfo();
            return await base.SaveChangesAsync(cancellationToken);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyAuditInfo();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ApplyAuditInfo()
        {
            var currentUserId = _currentUserService.UserId ?? 0;
            var now = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<AuditEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = now;
                        entry.Entity.CreatedBy = currentUserId;
                        entry.Entity.IsDeleted = false;
                        break;

                    case EntityState.Modified:
                        entry.Entity.ModifiedAt = now;
                        entry.Entity.ModifiedBy = currentUserId;
                        entry.Property(e => e.CreatedAt).IsModified = false;
                        entry.Property(e => e.CreatedBy).IsModified = false;
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedAt = now;
                        entry.Entity.DeletedBy = currentUserId;
                        break;
                }
            }
        }
    }
}