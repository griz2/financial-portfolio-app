namespace Portfolio.Core.Domain
{
    public class InvestmentPortfolio
    {
        public int Id { get; private set; }
        public int AccountId { get; private set; }
        public string Name { get; private set; } = string.Empty;

        private InvestmentPortfolio()
        {
        }

        public InvestmentPortfolio(int accountId, string name)
        {
            if (accountId <= 0)
            {
                throw new FinancialRuleException("A portfolio requires a valid financial account.");
            }

            AccountId = accountId;
            Name = FinancialValues.CheckName(name);
        }

        public void Rename(string name)
        {
            Name = FinancialValues.CheckName(name);
        }
    }
}
