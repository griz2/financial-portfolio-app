namespace Portfolio.Core.Domain
{
    public class FinancialRuleException : Exception
    {
        public FinancialRuleException(string message) : base(message)
        {
        }
    }
}
