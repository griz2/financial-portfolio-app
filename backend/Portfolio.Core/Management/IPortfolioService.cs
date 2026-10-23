namespace Portfolio.Core.Management
{
    public interface IPortfolioService
    {
        Task<List<PortfolioDetails>> GetPortfolios(string ownerId, int accountId);
        Task<PortfolioDetails> CreatePortfolio(string ownerId, int accountId, string name);
        Task<PortfolioDetails> RenamePortfolio(string ownerId, int accountId, int portfolioId, string name);
        Task<List<InvestmentDetails>> GetInvestments();
        Task<InvestmentDetails> CreateInvestment(string symbol, string name, decimal price);
        Task<InvestmentDetails> UpdateInvestment(int id, decimal price);
        Task<List<HoldingDetails>> GetHoldings(string ownerId, int accountId, int portfolioId);
        Task<TransactionDetails> RecordCash(string ownerId, int accountId, string type, decimal amount);
        Task<TransactionDetails> RecordTrade(string ownerId, int accountId, int portfolioId, int investmentId, string type, decimal quantity);
        Task<DashboardDetails> GetDashboard(string ownerId);
    }
}
