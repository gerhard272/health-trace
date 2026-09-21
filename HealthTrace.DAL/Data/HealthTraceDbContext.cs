using HealthTrace.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace HealthTrace.DAL.Data
{
    public class HealthTraceDbContext : DbContext
    {
        public HealthTraceDbContext(DbContextOptions<HealthTraceDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Symptom> Symptoms { get; set; }

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

                entity.HasIndex(s => s.UserId); //questo ottimizza la ricerca per UserId

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(s => s.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}