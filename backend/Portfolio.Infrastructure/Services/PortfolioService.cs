using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Domain;
using Portfolio.Core.Management;
using Portfolio.Infrastructure.Data;

namespace Portfolio.Infrastructure.Services
{
    public class PortfolioService : IPortfolioService
    {
        private readonly PortfolioDbContext _context;

        public PortfolioService(PortfolioDbContext context)
        {
            _context = context;
        }

        public async Task<List<PortfolioDetails>> GetPortfolios(string ownerId, int accountId)
        {
            await FindAccount(ownerId, accountId);
            return await _context.Portfolios.AsNoTracking().Where(p => p.AccountId == accountId)
                .OrderBy(p => p.Id).Select(p => new PortfolioDetails()
                {
                    Id = p.Id,
                    AccountId = p.AccountId,
                    Name = p.Name
                }).ToListAsync();
        }

        public async Task<PortfolioDetails> CreatePortfolio(string ownerId, int accountId, string name)
        {
            FinancialAccount account = await FindAccount(ownerId, accountId);
            InvestmentPortfolio portfolio = new InvestmentPortfolio(accountId, name);
            _context.Portfolios.Add(portfolio);
            await _context.SaveChangesAsync();
            return new PortfolioDetails()
            {
                Id = portfolio.Id,
                AccountId = accountId,
                Name = portfolio.Name
            };
        }

        public async Task<PortfolioDetails> RenamePortfolio(string ownerId, int accountId, int portfolioId, string name)
        {
            FinancialAccount account = await FindAccount(ownerId, accountId);
            InvestmentPortfolio portfolio = await FindPortfolio(accountId, portfolioId);
            portfolio.Rename(name);
            await _context.SaveChangesAsync();
            return new PortfolioDetails()
            {
                Id = portfolio.Id,
                AccountId = accountId,
                Name = portfolio.Name
            };
        }

        public async Task<List<InvestmentDetails>> GetInvestments()
        {
            List<Investment> investments = await _context.Investments.AsNoTracking().OrderBy(i => i.Symbol).ToListAsync();
            List<InvestmentDetails> results = new List<InvestmentDetails>();
            foreach (Investment investment in investments)
            {
                results.Add(InvestmentResult(investment));
            }
            return results;
        }

        public async Task<InvestmentDetails> CreateInvestment(string symbol, string name, decimal price)
        {
            Investment investment = new Investment(symbol, name, price);
            _context.Investments.Add(investment);
            await _context.SaveChangesAsync();
            return InvestmentResult(investment);
        }

        public async Task<InvestmentDetails> UpdateInvestment(int id, decimal price)
        {
            Investment? investment = await _context.Investments.FindAsync(id);

            if (investment == null)
            {
                throw new ResourceNotFoundException();
            }

            investment.UpdatePrice(price);
            await _context.SaveChangesAsync();
            return InvestmentResult(investment);
        }

        public async Task<List<HoldingDetails>> GetHoldings(string ownerId, int accountId, int portfolioId)
        {
            await FindAccount(ownerId, accountId);
            await FindPortfolio(accountId, portfolioId);
            var rows = await (from h in _context.Holdings.AsNoTracking()
                              join i in _context.Investments on h.InvestmentId equals i.Id
                              where h.PortfolioId == portfolioId && h.Quantity > 0
                              orderby i.Symbol
                              select new
                              {
                                  Holding = h,
                                  Investment = i
                              }).ToListAsync();
            List<HoldingDetails> holdings = new List<HoldingDetails>();

            foreach (var row in rows)
            {
                decimal value = decimal.Round(row.Holding.Quantity * row.Investment.CurrentPrice, 2, MidpointRounding.AwayFromZero);
                holdings.Add(new HoldingDetails()
                {
                    InvestmentId = row.Investment.Id,
                    Symbol = row.Investment.Symbol,
                    Quantity = row.Holding.Quantity,
                    CurrentPrice = row.Investment.CurrentPrice,
                    MarketValue = value
                });
            }

            return holdings;
        }

        public async Task<TransactionDetails> RecordCash(string ownerId, int accountId, string type, decimal amount)
        {
            FinancialAccount account = await FindAccount(ownerId, accountId);
            TransactionType transactionType;

            if (type == "Deposit")
            {
                transactionType = TransactionType.Deposit;
                account.Deposit(amount);
            }
            else if (type == "Withdrawal")
            {
                transactionType = TransactionType.Withdrawal;
                account.Withdraw(amount);
            }
            else
            {
                throw new FinancialRuleException("Choose Deposit or Withdrawal.");
            }

            FinancialTransaction transaction = FinancialTransaction.RecordCash(accountId, transactionType, amount, Guid.NewGuid());
            _context.FinancialTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return TransactionResult(transaction, null);
        }

        public async Task<TransactionDetails> RecordTrade(string ownerId, int accountId, int portfolioId,
            int investmentId, string type, decimal quantity)
        {
            FinancialAccount account = await FindAccount(ownerId, accountId);
            await FindPortfolio(accountId, portfolioId);
            Investment? investment = await _context.Investments.FindAsync(investmentId);

            if (investment == null)
            {
                throw new ResourceNotFoundException();
            }

            decimal gross = FinancialValues.TradeAmount(quantity, investment.CurrentPrice);
            Holding? holding = await _context.Holdings.FirstOrDefaultAsync(h =>
                h.PortfolioId == portfolioId && h.InvestmentId == investmentId);
            TransactionType transactionType;

            if (type == "Buy")
            {
                if (holding == null)
                {
                    holding = new Holding(portfolioId, investmentId);
                    _context.Holdings.Add(holding);
                }

                account.Withdraw(gross);
                holding.Buy(quantity, investment.CurrentPrice);
                transactionType = TransactionType.Buy;
            }
            else if (type == "Sell")
            {
                if (holding == null)
                {
                    throw new FinancialRuleException("This portfolio does not own that investment.");
                }

                decimal proceeds = holding.Sell(quantity, investment.CurrentPrice);
                account.Deposit(proceeds);
                transactionType = TransactionType.Sell;
            }
            else
            {
                throw new FinancialRuleException("Choose Buy or Sell.");
            }

            FinancialTransaction transaction = FinancialTransaction.RecordTrade(accountId, portfolioId,
                investmentId, transactionType, quantity, investment.CurrentPrice, Guid.NewGuid());
            _context.FinancialTransactions.Add(transaction);
            await _context.SaveChangesAsync();
            return TransactionResult(transaction, investment.Symbol);
        }

        public async Task<DashboardDetails> GetDashboard(string ownerId)
        {
            decimal cash = await _context.FinancialAccounts.Where(a => a.OwnerId == ownerId)
                .SumAsync(a => a.CashBalance);
            var positions = await (from h in _context.Holdings.AsNoTracking()
                                   join p in _context.Portfolios on h.PortfolioId equals p.Id
                                   join a in _context.FinancialAccounts on p.AccountId equals a.Id
                                   join i in _context.Investments on h.InvestmentId equals i.Id
                                   where a.OwnerId == ownerId && h.Quantity > 0
                                   select new { h.Quantity, i.CurrentPrice }).ToListAsync();
            decimal value = 0;

            foreach (var position in positions)
            {
                value += decimal.Round(position.Quantity * position.CurrentPrice, 2, MidpointRounding.AwayFromZero);
            }

            return new DashboardDetails()
            {
                CashBalance = cash,
                InvestmentValue = value,
                TotalValue = cash + value,
                AccountCount = await _context.FinancialAccounts.CountAsync(a => a.OwnerId == ownerId),
                PortfolioCount = await (from p in _context.Portfolios
                                        join a in _context.FinancialAccounts on p.AccountId equals a.Id
                                        where a.OwnerId == ownerId
                                        select p.Id).CountAsync()
            };
        }

        private async Task<FinancialAccount> FindAccount(string ownerId, int accountId)
        {
            FinancialAccount? account = await _context.FinancialAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == ownerId);

            if (account == null)
            {
                throw new ResourceNotFoundException();
            }

            return account;
        }

        private async Task<InvestmentPortfolio> FindPortfolio(int accountId, int portfolioId)
        {
            InvestmentPortfolio? portfolio = await _context.Portfolios
                .FirstOrDefaultAsync(p => p.Id == portfolioId && p.AccountId == accountId);

            if (portfolio == null)
            {
                throw new ResourceNotFoundException();
            }

            return portfolio;
        }

        private static InvestmentDetails InvestmentResult(Investment investment)
        {
            return new InvestmentDetails()
            {
                Id = investment.Id,
                Symbol = investment.Symbol,
                Name = investment.Name,
                CurrentPrice = investment.CurrentPrice
            };
        }

        private static TransactionDetails TransactionResult(FinancialTransaction transaction, string? symbol)
        {
            return new TransactionDetails()
            {
                Id = transaction.Id,
                AccountId = transaction.AccountId,
                PortfolioId = transaction.PortfolioId,
                Type = transaction.Type.ToString(),
                Quantity = transaction.Quantity,
                UnitPrice = transaction.UnitPrice,
                GrossAmount = transaction.GrossAmount,
                RecordedAt = transaction.RecordedAt,
                Symbol = symbol
            };
        }
    }
}
