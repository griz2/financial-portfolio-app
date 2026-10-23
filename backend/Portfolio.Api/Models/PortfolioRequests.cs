using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.Models
{
    public class CashRequest
    {
        [Required]
        public string Type { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class TradeRequest
    {
        [Required]
        public string Type { get; set; } = string.Empty;
        public int InvestmentId { get; set; }
        public decimal Quantity { get; set; }
    }

    public class InvestmentRequest
    {
        [Required, StringLength(12)]
        public string Symbol { get; set; } = string.Empty;
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    public class InvestmentUpdateRequest
    {
        public decimal Price { get; set; }
    }
}
