namespace Portfolio.Core.Domain
{
    public enum TransactionType
    {
        Deposit,
        Withdrawal,
        Buy,
        Sell
    }

    public class FinancialTransaction
    {
        public long Id { get; private set; }
        public int AccountId { get; private set; }
        public int? PortfolioId { get; private set; }
        public int? InvestmentId { get; private set; }
        public TransactionType Type { get; private set; }
        public decimal? Quantity { get; private set; }
        public decimal? UnitPrice { get; private set; }
        public decimal GrossAmount { get; private set; }
        public DateTime RecordedAt { get; private set; }
        public Guid RequestId { get; private set; }

        private FinancialTransaction()
        {
        }

        public static FinancialTransaction RecordCash(int accountId, TransactionType type,
            decimal amount, Guid requestId)
        {
            if (type != TransactionType.Deposit && type != TransactionType.Withdrawal)
            {
                throw new FinancialRuleException("A cash transaction must be a deposit or withdrawal.");
            }

            CheckIdentifiers(accountId, requestId);
            FinancialValues.CheckMoney(amount);

            return new FinancialTransaction()
            {
                AccountId = accountId,
                Type = type,
                GrossAmount = amount,
                RequestId = requestId,
                RecordedAt = DateTime.UtcNow
            };
        }

        public static FinancialTransaction RecordTrade(int accountId, int portfolioId,
            int investmentId, TransactionType type, decimal quantity, decimal unitPrice,
            Guid requestId)
        {
            CheckIdentifiers(accountId, requestId);

            if (portfolioId <= 0 || investmentId <= 0 ||
                (type != TransactionType.Buy && type != TransactionType.Sell))
            {
                throw new FinancialRuleException("A trade requires a valid portfolio, investment, and trade type.");
            }

            return new FinancialTransaction()
            {
                AccountId = accountId,
                PortfolioId = portfolioId,
                InvestmentId = investmentId,
                Type = type,
                Quantity = quantity,
                UnitPrice = unitPrice,
                GrossAmount = FinancialValues.TradeAmount(quantity, unitPrice),
                RequestId = requestId,
                RecordedAt = DateTime.UtcNow
            };
        }

        private static void CheckIdentifiers(int accountId, Guid requestId)
        {
            if (accountId <= 0 || requestId == Guid.Empty)
            {
                throw new FinancialRuleException("A transaction requires a valid account and request ID.");
            }
        }
    }
}
