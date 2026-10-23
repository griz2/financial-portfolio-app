using Portfolio.Core.Domain;

namespace Portfolio.Tests
{
    public class FinancialRulesTests
    {
        [Fact]
        public void FailedWithdrawalPreservesCash()
        {
            FinancialAccount account = new FinancialAccount("customer-1", "Investments");
            account.Deposit(100m);

            Assert.Throws<FinancialRuleException>(() => account.Withdraw(100.01m));
            Assert.Equal(100m, account.CashBalance);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("0.001")]
        [InlineData("10000000000000000")]
        public void InvalidDepositDoesNotChangeCash(string input)
        {
            FinancialAccount account = new FinancialAccount("customer-1", "Investments");
            decimal amount = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Throws<FinancialRuleException>(() => account.Deposit(amount));
            Assert.Equal(0m, account.CashBalance);
        }

        [Fact]
        public void BuyingAndSellingUpdatesQuantityAndReturnsProceeds()
        {
            Holding holding = new Holding(1, 1);
            holding.Buy(10m, 20m);
            holding.Buy(5m, 26m);
            decimal proceeds = holding.Sell(3m, 30m);
            Assert.Equal(90m, proceeds);
            Assert.Equal(12m, holding.Quantity);
        }

        [Fact]
        public void OversellingDoesNotChangeHolding()
        {
            Holding holding = new Holding(1, 1);
            holding.Buy(2m, 10m);

            Assert.Throws<FinancialRuleException>(() => holding.Sell(3m, 12m));
            Assert.Equal(2m, holding.Quantity);
        }

        [Fact]
        public void SellingAllUnitsLeavesZeroQuantity()
        {
            Holding holding = new Holding(1, 1);
            holding.Buy(3m, 0.333333m);
            holding.Sell(1m, 1m);
            holding.Sell(1m, 1m);
            holding.Sell(1m, 1m);

            Assert.Equal(0m, holding.Quantity);
        }

        [Fact]
        public void SubCentTradeIsRejectedWithoutCreatingUnits()
        {
            Holding holding = new Holding(1, 1);

            Assert.Throws<FinancialRuleException>(() => holding.Buy(0.000001m, 1m));
            Assert.Equal(0m, holding.Quantity);
        }

        [Fact]
        public void TradeUsesDocumentedMidpointRounding()
        {
            Assert.Equal(1.01m, FinancialValues.TradeAmount(1m, 1.005m));
        }
    }
}
