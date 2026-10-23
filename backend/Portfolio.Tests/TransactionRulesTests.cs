using Portfolio.Core.Domain;

namespace Portfolio.Tests
{
    public class TransactionRulesTests
    {
        [Fact]
        public void CashHistoryRequiresCashTypeAndRequestId()
        {
            Assert.Throws<FinancialRuleException>(() => FinancialTransaction.RecordCash(1,
                TransactionType.Buy, 10m, Guid.NewGuid()));
            Assert.Throws<FinancialRuleException>(() => FinancialTransaction.RecordCash(1,
                TransactionType.Deposit, 10m, Guid.Empty));
        }

        [Fact]
        public void TradeHistoryCapturesExecutionPriceIndependentlyOfCatalogPrice()
        {
            Investment investment = new Investment(" sample ", "Sample investment", 20m);
            FinancialTransaction transaction = FinancialTransaction.RecordTrade(1, 2, 3,
                TransactionType.Buy, 5m, investment.CurrentPrice, Guid.NewGuid());
            investment.UpdatePrice(25m);

            Assert.Equal("SAMPLE", investment.Symbol);
            Assert.Equal(20m, transaction.UnitPrice);
            Assert.Equal(100m, transaction.GrossAmount);
            Assert.Equal(25m, investment.CurrentPrice);
            Assert.Equal(DateTimeKind.Utc, transaction.RecordedAt.Kind);
        }

        [Fact]
        public void SaleHistoryRecordsQuantityPriceAndAmount()
        {
            FinancialTransaction sale = FinancialTransaction.RecordTrade(1, 2, 3,
                TransactionType.Sell, 2m, 10m, Guid.NewGuid());
            Assert.Equal(20m, sale.GrossAmount);
            Assert.Equal(2m, sale.Quantity);
        }
    }
}
