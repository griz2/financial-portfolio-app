using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Domain;
using Portfolio.Infrastructure.Identity;

namespace Portfolio.Infrastructure.Data
{
    public class PortfolioDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<FinancialAccount> FinancialAccounts { get; set; }
        public DbSet<InvestmentPortfolio> Portfolios { get; set; }
        public DbSet<Investment> Investments { get; set; }
        public DbSet<Holding> Holdings { get; set; }
        public DbSet<FinancialTransaction> FinancialTransactions { get; set; }

        public PortfolioDbContext(DbContextOptions<PortfolioDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<FinancialAccount>(entity =>
            {
                entity.Property(a => a.Name).HasMaxLength(100);
                entity.Property(a => a.OwnerId).HasMaxLength(450);
                entity.Property(a => a.CashBalance).HasPrecision(18, 2);
                entity.Property(a => a.RowVersion).IsRowVersion();
                entity.HasIndex(a => a.OwnerId);
                entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.OwnerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable("FinancialAccounts", table =>
                    table.HasCheckConstraint("CK_Account_Cash", "[CashBalance] >= 0"));
            });

            modelBuilder.Entity<InvestmentPortfolio>(entity =>
            {
                entity.Property(p => p.Name).HasMaxLength(100);
                entity.HasAlternateKey(p => new { p.AccountId, p.Id });
                entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(p => p.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Investment>(entity =>
            {
                entity.Property(i => i.Symbol).HasMaxLength(12);
                entity.Property(i => i.Name).HasMaxLength(100);
                entity.Property(i => i.CurrentPrice).HasPrecision(18, 6);
                entity.Property(i => i.RowVersion).IsRowVersion();
                entity.HasIndex(i => i.Symbol).IsUnique();
                entity.ToTable("Investments", table =>
                    table.HasCheckConstraint("CK_Investment_Price", "[CurrentPrice] > 0"));
            });

            modelBuilder.Entity<Holding>(entity =>
            {
                entity.Property(h => h.Quantity).HasPrecision(18, 6);
                entity.Property(h => h.RowVersion).IsRowVersion();
                entity.HasIndex(h => new { h.PortfolioId, h.InvestmentId }).IsUnique();
                entity.HasOne<InvestmentPortfolio>().WithMany().HasForeignKey(h => h.PortfolioId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Investment>().WithMany().HasForeignKey(h => h.InvestmentId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable("Holdings", table =>
                {
                    table.HasCheckConstraint("CK_Holding_Amounts", "[Quantity] >= 0");
                });
            });

            modelBuilder.Entity<FinancialTransaction>(entity =>
            {
                entity.Property(t => t.Type).HasConversion<string>().HasMaxLength(20);
                entity.Property(t => t.Quantity).HasPrecision(18, 6);
                entity.Property(t => t.UnitPrice).HasPrecision(18, 6);
                entity.Property(t => t.GrossAmount).HasPrecision(18, 2);
                entity.HasIndex(t => new { t.AccountId, t.RequestId }).IsUnique();
                entity.HasIndex(t => new { t.AccountId, t.RecordedAt, t.Id });
                entity.HasOne<FinancialAccount>().WithMany().HasForeignKey(t => t.AccountId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<InvestmentPortfolio>().WithMany()
                    .HasForeignKey(t => new { t.AccountId, t.PortfolioId })
                    .HasPrincipalKey(p => new { p.AccountId, p.Id })
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Investment>().WithMany().HasForeignKey(t => t.InvestmentId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.ToTable("FinancialTransactions", table =>
                {
                    table.HasCheckConstraint("CK_Transaction_Request", "[RequestId] <> '00000000-0000-0000-0000-000000000000'");
                    table.HasCheckConstraint("CK_Transaction_Amount", "[GrossAmount] > 0");
                    table.HasCheckConstraint("CK_Transaction_Shape",
                        "([Type] IN ('Deposit', 'Withdrawal') AND [PortfolioId] IS NULL AND [InvestmentId] IS NULL AND [Quantity] IS NULL AND [UnitPrice] IS NULL) OR " +
                        "([Type] IN ('Buy', 'Sell') AND [PortfolioId] IS NOT NULL AND [InvestmentId] IS NOT NULL AND [Quantity] IS NOT NULL AND [Quantity] > 0 AND [UnitPrice] IS NOT NULL AND [UnitPrice] > 0)");
                });
            });
        }
    }
}
