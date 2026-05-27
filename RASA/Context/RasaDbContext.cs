using Microsoft.EntityFrameworkCore;
using RasaApi.Models;

namespace RasaApi.Data
{
    public class RasaDbContext : DbContext
    {
        public RasaDbContext(DbContextOptions<RasaDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Connection> Connections { get; set; } = null!;
        public DbSet<ElderlyLocation> Locations { get; set; } = null!;
        public DbSet<ElderlyActivity> Activities { get; set; } = null!;
        public DbSet<Alert> Alerts { get; set; } = null!;
        public DbSet<EnvironmentRecord> EnvironmentRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Alert>()
                .HasQueryFilter(alert => !alert.IsDeleted);

            modelBuilder.Entity<Connection>()
                .HasIndex(connection => connection.ElderlyId)
                .IsUnique()
                .HasFilter("status IN ('pending', 'accepted')");

            modelBuilder.Entity<Connection>()
                .HasIndex(connection => connection.FamilyId)
                .IsUnique()
                .HasFilter("status IN ('pending', 'accepted')");
        }
    }
}