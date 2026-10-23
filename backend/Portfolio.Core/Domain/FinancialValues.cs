namespace Portfolio.Core.Domain
{
    public static class FinancialValues
    {
        public const decimal MaximumMoney = 9999999999999999.99m;
        public const decimal MaximumUnitsOrPrice = 999999999999.999999m;

        public static void CheckMoney(decimal amount)
        {
            if (amount <= 0 || amount > MaximumMoney || decimal.Round(amount, 2) != amount)
            {
                throw new FinancialRuleException("Amount must be positive, within range, and have at most two decimal places.");
            }
        }

        public static void CheckUnitsOrPrice(decimal value)
        {
            if (value <= 0 || value > MaximumUnitsOrPrice || decimal.Round(value, 6) != value)
            {
                throw new FinancialRuleException("Quantity and price must be positive, within range, and have at most six decimal places.");
            }
        }

        public static decimal TradeAmount(decimal quantity, decimal price)
        {
            CheckUnitsOrPrice(quantity);
            CheckUnitsOrPrice(price);

            decimal gross = decimal.Round(quantity * price, 2, MidpointRounding.AwayFromZero);
            CheckMoney(gross);
            return gross;
        }

        public static string CheckName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            {
                throw new FinancialRuleException("Name must contain 1 to 100 characters.");
            }

            return name.Trim();
        }
    }
}
