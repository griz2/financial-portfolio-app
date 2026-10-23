namespace Portfolio.Core.Management
{
    public class HoldingDetails
    {
        public int InvestmentId { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
    }
}
