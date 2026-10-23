using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using Portfolio.Core.Accounts;
using Portfolio.Core.Domain;
using Portfolio.Core.Management;
using Portfolio.Infrastructure.Data;

namespace Portfolio.Tests.Integration
{
    [Trait("Category", "SqlServer")]
    public class PortfolioWorkflowTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _database;
        private const string Password = "Workflow-Testing-2026!";

        public PortfolioWorkflowTests(SqlServerFixture database) { _database = database; }

        [Fact]
        public async Task FullWorkflowPersistsCashHoldingsHistoryAndDashboard()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            AccountDetails account = await CreateAccount(client);
            PortfolioDetails portfolio = await CreatePortfolio(client, account.Id);
            string portfolioUrl = $"/api/accounts/{account.Id}/portfolios/{portfolio.Id}";
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(portfolioUrl, new { name = "Long term" })).StatusCode);
            Assert.Equal("Long term", (await client.GetFromJsonAsync<PortfolioDetails>(portfolioUrl))!.Name);

            int investmentId = await CreateInvestment(20m);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync($"/api/accounts/{account.Id}/cash-transactions", new { type = "Deposit", amount = 1000m })).StatusCode);
            HttpResponseMessage buy = await client.PostAsJsonAsync(portfolioUrl + "/trades", new { type = "Buy", investmentId, quantity = 10m, unitPrice = 0.01m });
            Assert.Equal(HttpStatusCode.Created, buy.StatusCode);
            Assert.Equal(20m, (await buy.Content.ReadFromJsonAsync<TransactionDetails>())!.UnitPrice);

            await using (PortfolioDbContext context = _database.CreateContext())
            {
                (await context.Investments.SingleAsync(i => i.Id == investmentId)).UpdatePrice(25m);
                await context.SaveChangesAsync();
            }

            HttpResponseMessage sell = await client.PostAsJsonAsync(portfolioUrl + "/trades", new { type = "Sell", investmentId, quantity = 4m });
            Assert.Equal(HttpStatusCode.Created, sell.StatusCode);
            HoldingDetails holding = (await client.GetFromJsonAsync<List<HoldingDetails>>(portfolioUrl + "/holdings"))!.Single();
            Assert.Equal(6m, holding.Quantity);
            Assert.Equal(150m, holding.MarketValue);
            DashboardDetails dashboard = (await client.GetFromJsonAsync<DashboardDetails>("/api/dashboard"))!;
            Assert.Equal(900m, dashboard.CashBalance);
            Assert.Equal(150m, dashboard.InvestmentValue);
            Assert.Equal(1050m, dashboard.TotalValue);
            Assert.Equal(1, dashboard.PortfolioCount);

            List<TransactionDetails> history = (await client.GetFromJsonAsync<List<TransactionDetails>>($"/api/accounts/{account.Id}/history"))!;
            Assert.Equal(3, history.Count);
            Assert.Equal("Sell", history[0].Type);
            Assert.Equal(20m, history.Single(t => t.Type == "Buy").UnitPrice);
        }

        [Fact]
        public async Task FailedFinancialCommandsDoNotPersistPartialChanges()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            AccountDetails account = await CreateAccount(client);
            PortfolioDetails portfolio = await CreatePortfolio(client, account.Id);
            int investmentId = await CreateInvestment(10m);
            string cashUrl = $"/api/accounts/{account.Id}/cash-transactions";
            string tradeUrl = $"/api/accounts/{account.Id}/portfolios/{portfolio.Id}/trades";
            foreach (decimal amount in new[] { -1m, 0m, 0.001m })
            {
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(cashUrl, new { type = "Deposit", amount })).StatusCode);
            }

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(cashUrl, new { type = "Withdrawal", amount = 1m })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(tradeUrl, new { type = "Buy", investmentId, quantity = 1m })).StatusCode);
            await client.PostAsJsonAsync(cashUrl, new { type = "Deposit", amount = 100m });
            await client.PostAsJsonAsync(tradeUrl, new { type = "Buy", investmentId, quantity = 2m });
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(tradeUrl, new { type = "Sell", investmentId, quantity = 3m })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(tradeUrl, new { type = "Buy", investmentId, quantity = 0.0000001m })).StatusCode);
            Assert.Equal(80m, (await client.GetFromJsonAsync<AccountDetails>($"/api/accounts/{account.Id}"))!.CashBalance);
            Assert.Equal(2, (await client.GetFromJsonAsync<List<TransactionDetails>>($"/api/accounts/{account.Id}/history"))!.Count);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync(cashUrl, new { type = "Withdrawal", amount = 30m })).StatusCode);
            Assert.Equal(50m, (await client.GetFromJsonAsync<AccountDetails>($"/api/accounts/{account.Id}"))!.CashBalance);
        }

        [Fact]
        public async Task OwnershipIsEnforcedForAccountsPortfoliosTradesAndAdoReport()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient owner = application.CreateHttpsClient();
            using HttpClient other = application.CreateHttpsClient();
            await RegisterAndLogin(owner);
            AccountDetails account = await CreateAccount(owner);
            PortfolioDetails portfolio = await CreatePortfolio(owner, account.Id);
            Assert.Equal(HttpStatusCode.Created, (await owner.PostAsJsonAsync($"/api/accounts/{account.Id}/cash-transactions", new { type = "Deposit", amount = 100m })).StatusCode);
            await RegisterAndLogin(other);
            Assert.Empty((await other.GetFromJsonAsync<List<AccountDetails>>("/api/accounts"))!);
            foreach (string path in new[] { $"/api/accounts/{account.Id}", $"/api/accounts/{account.Id}/portfolios", $"/api/accounts/{account.Id}/portfolios/{portfolio.Id}/holdings", $"/api/accounts/{account.Id}/history" })
            {
                Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync(path)).StatusCode);
            }

            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/accounts/{account.Id}", new { name = "Stolen" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/accounts/{account.Id}/cash-transactions", new { type = "Deposit", amount = 100m })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsJsonAsync($"/api/accounts/{account.Id}/portfolios/{portfolio.Id}/trades", new { type = "Buy", investmentId = 1, quantity = 1m })).StatusCode);

            AccountDetails otherAccount = await CreateAccount(other);
            JsonElement otherUser = await other.GetFromJsonAsync<JsonElement>("/api/auth/me");
            using var scope = application.Services.CreateScope();
            List<TransactionDetails> directReport = await scope.ServiceProvider.GetRequiredService<IHistoryReport>()
                .GetHistory(otherUser.GetProperty("id").GetString()!, account.Id);
            Assert.Empty(directReport);
            Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/accounts/{otherAccount.Id}/portfolios/{portfolio.Id}", new { name = "Wrong account" })).StatusCode);
        }

        [Fact]
        public async Task OnlyAdminCanMaintainCatalogPrices()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient customer = application.CreateHttpsClient();
            using HttpClient admin = application.CreateHttpsClient();
            await RegisterAndLogin(customer);
            int existingId = await CreateInvestment(10m);
            Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/investments", new { symbol = "NO", name = "Denied", price = 1m })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await customer.PutAsJsonAsync("/api/investments/" + existingId, new { price = 1m })).StatusCode);

            string adminEmail = NewEmail();
            using (var scope = application.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().CreateAdmin(adminEmail, Password);
            }

            await Login(admin, adminEmail);
            string symbol = "T" + Guid.NewGuid().ToString("N")[..8];
            HttpResponseMessage create = await admin.PostAsJsonAsync("/api/investments", new { symbol, name = "Test investment", price = 10m });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            int investmentId = (await create.Content.ReadFromJsonAsync<InvestmentDetails>())!.Id;
            Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync("/api/investments", new { symbol, name = "Duplicate", price = 10m })).StatusCode);
            AccountDetails account = await CreateAccount(customer);
            PortfolioDetails portfolio = await CreatePortfolio(customer, account.Id);
            await customer.PostAsJsonAsync($"/api/accounts/{account.Id}/cash-transactions", new { type = "Deposit", amount = 100m });
            string tradeUrl = $"/api/accounts/{account.Id}/portfolios/{portfolio.Id}/trades";
            Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync(tradeUrl, new { type = "Buy", investmentId, quantity = 2m })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync("/api/investments/" + investmentId, new { price = 12m })).StatusCode);
            Assert.Equal(12m, (await customer.GetFromJsonAsync<List<InvestmentDetails>>("/api/investments"))!.Single(i => i.Id == investmentId).CurrentPrice);
            Assert.Equal(HttpStatusCode.Created, (await customer.PostAsJsonAsync(tradeUrl, new { type = "Sell", investmentId, quantity = 2m })).StatusCode);
        }

        [Fact]
        public async Task HistoryReturnsAllTransactionsNewestFirst()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            AccountDetails account = await CreateAccount(client);
            await using (PortfolioDbContext context = _database.CreateContext())
            {
                FinancialAccount entity = await context.FinancialAccounts.SingleAsync(a => a.Id == account.Id);
                for (int i = 0; i < 23; i++)
                {
                    entity.Deposit(1m);
                    context.FinancialTransactions.Add(FinancialTransaction.RecordCash(account.Id, TransactionType.Deposit, 1m, Guid.NewGuid()));
                }
                await context.SaveChangesAsync();
            }

            string url = $"/api/accounts/{account.Id}/history";
            List<TransactionDetails> history = (await client.GetFromJsonAsync<List<TransactionDetails>>(url))!;
            Assert.Equal(23, history.Count);
            Assert.Equal(history.OrderByDescending(t => t.RecordedAt).ThenByDescending(t => t.Id).Select(t => t.Id),
                history.Select(t => t.Id));
        }

        [Fact]
        public async Task FinancialEndpointsRequireAuthenticationAndCsrf()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/accounts")).StatusCode);
            await RegisterAndLogin(client);
            AccountDetails account = await CreateAccount(client);
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            HttpResponseMessage dashboard = await client.GetAsync("/api/dashboard");
            Assert.True(dashboard.Headers.CacheControl?.NoStore);
            HttpResponseMessage accounts = await client.GetAsync("/api/accounts");
            Assert.True(accounts.Headers.CacheControl?.NoStore);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/accounts/{account.Id}/cash-transactions", new { type = "Deposit", amount = 100m })).StatusCode);
        }

        private static string NewEmail() { return Guid.NewGuid().ToString("N") + "@example.test"; }

        [Fact]
        public async Task DevelopmentHttpLoginWorksWithoutWeakeningProductionCookieConfiguration()
        {
            using PortfolioApplication application = new PortfolioApplication(_database.ConnectionString, "Development");
            using HttpClient client = application.CreateClient(new WebApplicationFactoryClientOptions()
            {
                BaseAddress = new Uri("http://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });
            await RegisterAndLogin(client);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/dashboard")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/demo/accounts")).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/accounts")).StatusCode);
        }
        private static async Task RefreshCsrf(HttpClient client)
        {
            JsonElement csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        }
        private static async Task RegisterAndLogin(HttpClient client)
        {
            string email = NewEmail();
            await RefreshCsrf(client);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).StatusCode);
            await Login(client, email);
        }
        private static async Task Login(HttpClient client, string email)
        {
            await RefreshCsrf(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);
            await RefreshCsrf(client);
        }
        private static async Task<AccountDetails> CreateAccount(HttpClient client)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts", new { name = "Investment account" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<AccountDetails>())!;
        }
        private static async Task<PortfolioDetails> CreatePortfolio(HttpClient client, int accountId)
        {
            HttpResponseMessage response = await client.PostAsJsonAsync($"/api/accounts/{accountId}/portfolios", new { name = "Growth" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<PortfolioDetails>())!;
        }
        private async Task<int> CreateInvestment(decimal price)
        {
            await using PortfolioDbContext context = _database.CreateContext();
            Investment investment = new Investment("T" + Guid.NewGuid().ToString("N")[..8], "Example", price);
            context.Investments.Add(investment); await context.SaveChangesAsync(); return investment.Id;
        }
    }
}
