using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Domain;
using Portfolio.Infrastructure.Data;
using Portfolio.Infrastructure.Identity;

namespace Portfolio.Tests.Integration
{
    [Trait("Category", "SqlServer")]
    public class PersistenceTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _database;

        public PersistenceTests(SqlServerFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task AccountAndHoldingRoundTripWithDecimalPrecisionAndRowVersion()
        {
            int accountId = await CreateAccount();
            int holdingId;

            await using (PortfolioDbContext context = _database.CreateContext())
            {
                InvestmentPortfolio portfolio = new InvestmentPortfolio(accountId, "Long term");
                Investment investment = new Investment("T" + Guid.NewGuid().ToString("N")[..8], "Example", 12.345678m);
                context.Portfolios.Add(portfolio);
                context.Investments.Add(investment);
                await context.SaveChangesAsync();

                Holding holding = new Holding(portfolio.Id, investment.Id);
                holding.Buy(1.234567m, 12.345678m);
                context.Holdings.Add(holding);
                await context.SaveChangesAsync();
                holdingId = holding.Id;
            }

            await using PortfolioDbContext verification = _database.CreateContext();
            Holding saved = await verification.Holdings.SingleAsync(h => h.Id == holdingId);
            Assert.Equal(1.234567m, saved.Quantity);
            Assert.NotEmpty(saved.RowVersion);
            FinancialAccount account = await verification.FinancialAccounts.SingleAsync(a => a.Id == accountId);
            Assert.Equal(100m, account.CashBalance);
            Assert.NotEmpty(account.RowVersion);
        }

        [Fact]
        public async Task StaleAccountWriteCannotOverwriteAnotherWithdrawal()
        {
            int accountId = await CreateAccount();
            await using PortfolioDbContext first = _database.CreateContext();
            await using PortfolioDbContext second = _database.CreateContext();
            FinancialAccount firstCopy = await first.FinancialAccounts.SingleAsync(a => a.Id == accountId);
            FinancialAccount secondCopy = await second.FinancialAccounts.SingleAsync(a => a.Id == accountId);

            firstCopy.Withdraw(80m);
            secondCopy.Withdraw(80m);
            await first.SaveChangesAsync();
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

            await using PortfolioDbContext verification = _database.CreateContext();
            Assert.Equal(20m, (await verification.FinancialAccounts.SingleAsync(a => a.Id == accountId)).CashBalance);
        }

        [Fact]
        public async Task DuplicateRequestRollsBackCashChangeAndHistoryTogether()
        {
            int accountId = await CreateAccount();
            Guid requestId = Guid.NewGuid();

            await using (PortfolioDbContext context = _database.CreateContext())
            {
                FinancialAccount account = await context.FinancialAccounts.SingleAsync(a => a.Id == accountId);
                account.Deposit(10m);
                context.FinancialTransactions.Add(FinancialTransaction.RecordCash(accountId, TransactionType.Deposit, 10m, requestId));
                context.FinancialTransactions.Add(FinancialTransaction.RecordCash(accountId, TransactionType.Deposit, 10m, requestId));

                await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            }

            await using PortfolioDbContext verification = _database.CreateContext();
            Assert.Equal(100m, (await verification.FinancialAccounts.SingleAsync(a => a.Id == accountId)).CashBalance);
            Assert.False(await verification.FinancialTransactions.AnyAsync(t => t.AccountId == accountId));
        }

        [Fact]
        public async Task DatabaseRejectsTradeLinkedToAnotherAccountsPortfolio()
        {
            int accountId = await CreateAccount();
            int otherAccountId = await CreateAccount();

            await using PortfolioDbContext context = _database.CreateContext();
            InvestmentPortfolio portfolio = new InvestmentPortfolio(otherAccountId, "Other account");
            Investment investment = new Investment("T" + Guid.NewGuid().ToString("N")[..8], "Example", 10m);
            context.Portfolios.Add(portfolio);
            context.Investments.Add(investment);
            await context.SaveChangesAsync();

            context.FinancialTransactions.Add(FinancialTransaction.RecordTrade(accountId, portfolio.Id,
                investment.Id, TransactionType.Buy, 1m, 10m, Guid.NewGuid()));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task DatabaseRejectsSecondHoldingForSamePortfolioAndInvestment()
        {
            int accountId = await CreateAccount();
            await using PortfolioDbContext context = _database.CreateContext();
            InvestmentPortfolio portfolio = new InvestmentPortfolio(accountId, "Long term");
            Investment investment = new Investment("T" + Guid.NewGuid().ToString("N")[..8], "Example", 10m);
            context.Portfolios.Add(portfolio);
            context.Investments.Add(investment);
            await context.SaveChangesAsync();

            context.Holdings.Add(new Holding(portfolio.Id, investment.Id));
            context.Holdings.Add(new Holding(portfolio.Id, investment.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        private async Task<int> CreateAccount()
        {
            await using PortfolioDbContext context = _database.CreateContext();
            ApplicationUser owner = new ApplicationUser()
            {
                UserName = Guid.NewGuid().ToString("N")
            };
            context.Users.Add(owner);
            FinancialAccount account = new FinancialAccount(owner.Id, "Test account");
            account.Deposit(100m);
            context.FinancialAccounts.Add(account);
            await context.SaveChangesAsync();
            return account.Id;
        }
    }
}
