namespace Portfolio.Core.Domain
{
    public class Holding
    {
        public int Id { get; private set; }
        public int PortfolioId { get; private set; }
        public int InvestmentId { get; private set; }
        public decimal Quantity { get; private set; }
        public byte[] RowVersion { get; private set; } = [];

        private Holding()
        {
        }

        public Holding(int portfolioId, int investmentId)
        {
            if (portfolioId <= 0 || investmentId <= 0)
            {
                throw new FinancialRuleException("A holding requires a valid portfolio and investment.");
            }

            PortfolioId = portfolioId;
            InvestmentId = investmentId;
        }

        public decimal Buy(decimal quantity, decimal price)
        {
            decimal gross = FinancialValues.TradeAmount(quantity, price);

            if (Quantity > FinancialValues.MaximumUnitsOrPrice - quantity)
            {
                throw new FinancialRuleException("The resulting holding exceeds the supported range.");
            }

            Quantity += quantity;
            return gross;
        }

        public decimal Sell(decimal quantity, decimal price)
        {
            decimal gross = FinancialValues.TradeAmount(quantity, price);

            if (quantity > Quantity)
            {
                throw new FinancialRuleException("Cannot sell more units than this portfolio owns.");
            }

            Quantity -= quantity;
            return gross;
        }
    }

}
