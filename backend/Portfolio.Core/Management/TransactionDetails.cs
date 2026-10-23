namespace Portfolio.Core.Management
{
    public class TransactionDetails
    {
        public long Id { get; set; }
        public int AccountId { get; set; }
        public int? PortfolioId { get; set; }
        public string? Symbol { get; set; }
        public string Type { get; set; } = string.Empty;
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal GrossAmount { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
