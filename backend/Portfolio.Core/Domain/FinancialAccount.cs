namespace Portfolio.Core.Domain
{
    public class FinancialAccount
    {
        public int Id { get; private set; }
        public string OwnerId { get; private set; } = string.Empty;
        public string Name { get; private set; } = string.Empty;
        public decimal CashBalance { get; private set; }
        public byte[] RowVersion { get; private set; } = [];

        private FinancialAccount()
        {
        }

        public FinancialAccount(string ownerId, string name)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                throw new FinancialRuleException("An account must have an owner.");
            }

            OwnerId = ownerId;
            Name = FinancialValues.CheckName(name);
        }

        public void Rename(string name)
        {
            Name = FinancialValues.CheckName(name);
        }

        public void Deposit(decimal amount)
        {
            FinancialValues.CheckMoney(amount);

            if (CashBalance > FinancialValues.MaximumMoney - amount)
            {
                throw new FinancialRuleException("The resulting cash balance exceeds the supported range.");
            }

            CashBalance += amount;
        }

        public void Withdraw(decimal amount)
        {
            FinancialValues.CheckMoney(amount);

            if (amount > CashBalance)
            {
                throw new FinancialRuleException("Insufficient available cash.");
            }

            CashBalance -= amount;
        }
    }
}
