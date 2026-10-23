using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Accounts;
using Portfolio.Core.Domain;
using Portfolio.Infrastructure.Data;
using Portfolio.Infrastructure.Identity;

namespace Portfolio.Tests.Integration
{
    [Trait("Category", "SqlServer")]
    public class AccountTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _database;

        public AccountTests(SqlServerFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task CustomerCanCreateReadListAndRenameOwnAccount()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string ownerId = await RegisterAndLogin(client);

            HttpResponseMessage create = await client.PostAsJsonAsync("/api/accounts", new { name = "  Savings  " });
            Assert.Equal(HttpStatusCode.Created, create.StatusCode);
            AccountDetails created = (await create.Content.ReadFromJsonAsync<AccountDetails>())!;
            Assert.Equal("Savings", created.Name);
            Assert.Equal(0m, created.CashBalance);
            Assert.NotNull(create.Headers.Location);

            AccountDetails read = (await client.GetFromJsonAsync<AccountDetails>(create.Headers.Location))!;
            Assert.Equal(created.Id, read.Id);
            List<AccountDetails> accounts = (await client.GetFromJsonAsync<List<AccountDetails>>("/api/accounts"))!;
            Assert.Contains(accounts, a => a.Id == created.Id);

            HttpResponseMessage rename = await client.PutAsJsonAsync("/api/accounts/" + created.Id,
                new { name = "  Retirement  " });
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
            await using PortfolioDbContext verification = _database.CreateContext();
            FinancialAccount saved = await verification.FinancialAccounts.SingleAsync(a => a.Id == created.Id);
            Assert.Equal("Retirement", saved.Name);
            Assert.Equal(ownerId, saved.OwnerId);
            Assert.Equal(0m, saved.CashBalance);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task BlankNamesAreRejected(string name)
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/accounts", new { name })).StatusCode);
        }

        [Fact]
        public async Task AccountNameLengthIsValidatedByServer()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/accounts",
                new { name = new string('a', 101) })).StatusCode);
        }

        [Fact]
        public async Task RequestCannotChooseOwnerOrBalance()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string ownerId = await RegisterAndLogin(client);
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/accounts",
                new { name = "Personal account", ownerId = "someone-else", cashBalance = 1000m });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            AccountDetails created = (await response.Content.ReadFromJsonAsync<AccountDetails>())!;

            await using PortfolioDbContext context = _database.CreateContext();
            FinancialAccount account = await context.FinancialAccounts.SingleAsync(a => a.Id == created.Id);
            Assert.Equal(ownerId, account.OwnerId);
            Assert.Equal(0m, account.CashBalance);
        }

        [Fact]
        public async Task CustomerCannotReadOrRenameAnotherOwnersAccount()
        {
            int otherAccountId;
            await using (PortfolioDbContext context = _database.CreateContext())
            {
                ApplicationUser otherOwner = new ApplicationUser()
                {
                    UserName = Guid.NewGuid().ToString("N")
                };
                context.Users.Add(otherOwner);
                context.FinancialAccounts.Add(new FinancialAccount(otherOwner.Id, "Other owner"));
                await context.SaveChangesAsync();
                otherAccountId = await context.FinancialAccounts.Where(a => a.OwnerId == otherOwner.Id)
                    .Select(a => a.Id).SingleAsync();
            }

            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/accounts/" + otherAccountId)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/accounts/" + otherAccountId,
                new { name = "Changed" })).StatusCode);
            List<AccountDetails> list = (await client.GetFromJsonAsync<List<AccountDetails>>("/api/accounts"))!;
            Assert.DoesNotContain(list, a => a.Id == otherAccountId);
        }

        [Theory]
        [InlineData("Development")]
        [InlineData("Production")]
        [InlineData("Testing")]
        public async Task RemovedDemoEndpointsAreUnavailable(string environment)
        {
            using PortfolioApplication application = new PortfolioApplication(_database.ConnectionString, environment);
            using HttpClient client = application.CreateHttpsClient();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/demo/accounts")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/demo/accounts", new { name = "Blocked" })).StatusCode);
        }

        [Fact]
        public async Task MissingAccountReturnsNotFound()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RegisterAndLogin(client);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/accounts/2147483647")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/accounts/2147483647",
                new { name = "Missing" })).StatusCode);
        }

        private static async Task<string> RegisterAndLogin(HttpClient client)
        {
            string email = Guid.NewGuid().ToString("N") + "@example.test";
            string password = "Account-Testing-2026!";
            await RefreshCsrf(client);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register",
                new { email, password })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login",
                new { email, password })).StatusCode);
            await RefreshCsrf(client);
            JsonElement user = await client.GetFromJsonAsync<JsonElement>("/api/auth/me");
            return user.GetProperty("id").GetString()!;
        }

        private static async Task RefreshCsrf(HttpClient client)
        {
            JsonElement csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", csrf.GetProperty("token").GetString());
        }
    }
}
