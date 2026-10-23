using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Accounts;
using Portfolio.Core.Domain;
using Portfolio.Infrastructure.Data;

namespace Portfolio.Infrastructure.Services
{
    public class AccountService : IAccountService
    {
        private readonly PortfolioDbContext _context;

        public AccountService(PortfolioDbContext context)
        {
            _context = context;
        }

        public async Task<List<AccountDetails>> GetAccounts(string ownerId)
        {
            List<FinancialAccount> accounts = await _context.FinancialAccounts.AsNoTracking()
                .Where(a => a.OwnerId == ownerId).OrderBy(a => a.Id).ToListAsync();
            List<AccountDetails> results = new List<AccountDetails>();

            foreach (FinancialAccount account in accounts)
            {
                results.Add(GetDetails(account));
            }

            return results;
        }

        public async Task<AccountDetails?> GetAccount(string ownerId, int accountId)
        {
            FinancialAccount? account = await _context.FinancialAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == ownerId);

            if (account == null)
            {
                return null;
            }

            return GetDetails(account);
        }

        public async Task<AccountDetails> CreateAccount(string ownerId, string name)
        {
            FinancialAccount account = new FinancialAccount(ownerId, name);
            _context.FinancialAccounts.Add(account);
            await _context.SaveChangesAsync();

            return GetDetails(account);
        }

        public async Task<AccountDetails?> RenameAccount(string ownerId, int accountId, string name)
        {
            FinancialAccount? account = await _context.FinancialAccounts
                .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == ownerId);

            if (account == null)
            {
                return null;
            }

            account.Rename(name);
            await _context.SaveChangesAsync();
            return GetDetails(account);
        }

        private static AccountDetails GetDetails(FinancialAccount account)
        {
            return new AccountDetails()
            {
                Id = account.Id,
                Name = account.Name,
                CashBalance = account.CashBalance
            };
        }
    }
}
