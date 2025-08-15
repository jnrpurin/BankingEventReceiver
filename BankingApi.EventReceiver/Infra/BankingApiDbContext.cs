using BankingApi.EventReceiver.Models;
using Microsoft.EntityFrameworkCore;

namespace BankingApi.EventReceiver.Infra
{
    public class BankingApiDbContext : DbContext
    {
        public BankingApiDbContext(DbContextOptions<BankingApiDbContext> options) : base(options) { }

        public DbSet<BankAccount> BankAccounts { get; set; }
        public DbSet<ProcessedTransaction> ProcessedTransactions { get; set; }
        public DbSet<TransactionAuditLog> TransactionAuditLogs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!options.IsConfigured)
            {
                var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
                    ?? "Server=sqlserver,1433;Database=BankingApiTest;User Id=sa;Password=YourPassword123!;TrustServerCertificate=true;Encrypt=false;";

                options.UseSqlServer(connectionString);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BankAccount>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Balance)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.HasIndex(e => e.Id).IsUnique();
            });

            modelBuilder.Entity<ProcessedTransaction>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MessageId).IsRequired();
                entity.Property(e => e.BankAccountId).IsRequired();
                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.PreviousBalance)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.NewBalance)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.TransactionType).IsRequired();
                entity.Property(e => e.ProcessedAt).IsRequired();
                entity.HasIndex(e => e.MessageId).IsUnique();
            });

            modelBuilder.Entity<TransactionAuditLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.MessageId).IsRequired();
                entity.Property(e => e.BankAccountId).IsRequired();
                entity.Property(e => e.Amount)
                    .HasColumnType("decimal(18,2)")
                    .IsRequired();
                entity.Property(e => e.PreviousBalance)
                    .HasColumnType("decimal(18,2)");
                entity.Property(e => e.NewBalance)
                    .HasColumnType("decimal(18,2)");
                entity.Property(e => e.TransactionType).IsRequired();
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.ProcessedAt).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
                entity.HasIndex(e => e.MessageId);
                entity.HasIndex(e => e.BankAccountId);
                entity.HasIndex(e => e.ProcessedAt);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
