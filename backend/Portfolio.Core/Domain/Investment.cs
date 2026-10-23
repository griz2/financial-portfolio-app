namespace Portfolio.Core.Domain
{
    public class Investment
    {
        public int Id { get; private set; }
        public string Symbol { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public decimal CurrentPrice { get; private set; }
        public byte[] RowVersion { get; private set; } = [];

        private Investment()
        {
        }

        public Investment(string symbol, string name, decimal price)
        {
            if (string.IsNullOrWhiteSpace(symbol) || symbol.Trim().Length > 12)
            {
                throw new FinancialRuleException("Symbol must contain 1 to 12 characters.");
            }

            Symbol = symbol.Trim().ToUpperInvariant();
            Name = FinancialValues.CheckName(name);
            UpdatePrice(price);
        }

        public void UpdatePrice(decimal price)
        {
            FinancialValues.CheckUnitsOrPrice(price);
            CurrentPrice = price;
        }

    }
}
