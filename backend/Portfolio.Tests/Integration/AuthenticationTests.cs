using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Core.Authentication;
using Portfolio.Infrastructure.Data;
using Portfolio.Infrastructure.Identity;

namespace Portfolio.Tests.Integration
{
    [Trait("Category", "SqlServer")]
    public class AuthenticationTests : IClassFixture<SqlServerFixture>
    {
        private readonly SqlServerFixture _database;
        private const string TestPassword = "Portfolio-Test-2026!";

        public AuthenticationTests(SqlServerFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task RegistrationHashesPasswordAndIgnoresClientRole()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = NewEmail();
            await RefreshCsrf(client);

            HttpResponseMessage registration = await client.PostAsJsonAsync("/api/auth/register",
                new { email, password = TestPassword, role = "Admin", isActive = false });
            Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

            using var scope = application.Services.CreateScope();
            UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = (await manager.FindByEmailAsync(email))!;
            Assert.NotNull(user);
            Assert.NotEqual(TestPassword, user.PasswordHash);
            Assert.True(await manager.CheckPasswordAsync(user, TestPassword));
            Assert.True(await manager.IsInRoleAsync(user, ApplicationRoles.Customer));
            Assert.False(await manager.IsInRoleAsync(user, ApplicationRoles.Admin));
            Assert.True(user.IsActive);

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }

        [Fact]
        public async Task LoginAndLogoutUseSecureCookieAndIdentityBoundCsrfToken()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = await RegisterCustomer(client);

            HttpResponseMessage login = await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = TestPassword });
            Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
            string cookie = login.Headers.GetValues("Set-Cookie")
                .First(value => value.StartsWith("__Host-Portfolio.Session="));
            Assert.Contains("httponly", cookie.ToLowerInvariant());
            Assert.Contains("secure", cookie.ToLowerInvariant());
            Assert.Contains("samesite=strict", cookie.ToLowerInvariant());

            UserDetails? me = await client.GetFromJsonAsync<UserDetails>("/api/auth/me");
            Assert.Equal(email, me!.Email);
            Assert.Contains(ApplicationRoles.Customer, me.Roles);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
            await RefreshCsrf(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }

        [Fact]
        public async Task MissingOrForgedCsrfTokenRejectsAuthenticationWrites()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            object credentials = new { email = NewEmail(), password = TestPassword };

            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/auth/register", credentials)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await client.PostAsJsonAsync("/api/auth/login", credentials)).StatusCode);
            await RefreshCsrf(client);
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", "forged-token");
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register", credentials);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("Antiforgery validation failed", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvalidRegistrationAndDuplicateEmailDoNotCreateExtraUsers()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = NewEmail();
            await RefreshCsrf(client);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register",
                new { email, password = "short" })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/auth/register",
                new { email, password = TestPassword })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register",
                new { email = email.ToUpperInvariant(), password = TestPassword })).StatusCode);

            await using PortfolioDbContext context = _database.CreateContext();
            Assert.Equal(1, await context.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant()));
        }

        [Fact]
        public async Task RepeatedWrongPasswordLocksAccountAndKeepsFailureGeneric()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = await RegisterCustomer(client);

            for (int attempt = 0; attempt < 5; attempt++)
            {
                HttpResponseMessage failure = await client.PostAsJsonAsync("/api/auth/login",
                    new { email, password = "Wrong-Password-2026!" });
                Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
                Assert.Contains("Check your credentials or try again later", await failure.Content.ReadAsStringAsync());
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = TestPassword })).StatusCode);

            using var scope = application.Services.CreateScope();
            UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True(await manager.IsLockedOutAsync((await manager.FindByEmailAsync(email))!));
        }

        [Fact]
        public async Task DisablingUserRejectsExistingSessionAndNewLogin()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = await RegisterCustomer(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = TestPassword })).StatusCode);

            await using (PortfolioDbContext context = _database.CreateContext())
            {
                ApplicationUser user = await context.Users.SingleAsync(u => u.Email == email);
                user.IsActive = false;
                await context.SaveChangesAsync();
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
            await RefreshCsrf(client);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = TestPassword })).StatusCode);
        }

        [Fact]
        public async Task UpdatingSecurityStampRevokesExistingSession()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = await RegisterCustomer(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login",
                new { email, password = TestPassword })).StatusCode);

            using var scope = application.Services.CreateScope();
            UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await manager.UpdateSecurityStampAsync((await manager.FindByEmailAsync(email))!)).Succeeded);

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }

        [Fact]
        public async Task AuthenticationEndpointsLimitRepeatedRequests()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            await RefreshCsrf(client);

            for (int attempt = 0; attempt < 20; attempt++)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login",
                    new { email = "missing@example.test", password = TestPassword })).StatusCode);
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login",
                new { email = "missing@example.test", password = TestPassword })).StatusCode);
        }

        private static string NewEmail()
        {
            return "customer-" + Guid.NewGuid().ToString("N") + "@example.test";
        }

        [Fact]
        public async Task AdministratorProvisioningRejectsPasswordLongerThanLoginAccepts()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using var scope = application.Services.CreateScope();
            string email = NewEmail();
            DatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.CreateAdmin(email, TestPassword.PadRight(129, 'a')));
            Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email));
        }

        [Theory]
        [InlineData("not-an-email")]
        [InlineData(" ")]
        public async Task AdministratorProvisioningRejectsInvalidEmail(string email)
        {
            using PortfolioApplication application = _database.CreateApplication();
            using var scope = application.Services.CreateScope();
            await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
                .GetRequiredService<DatabaseInitializer>().CreateAdmin(email, TestPassword));
        }

        [Fact]
        public async Task AdministratorCanLoginWithMaximumSupportedPasswordLength()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string email = NewEmail();
            string password = TestPassword.PadRight(128, 'a');
            using (var scope = application.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().CreateAdmin(email, password);
            }
            await RefreshCsrf(client);
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/login", new { email, password })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        }

        [Fact]
        public async Task AdministratorProvisioningCreatesRolesButRefusesExistingUserPromotion()
        {
            using PortfolioApplication application = _database.CreateApplication();
            using HttpClient client = application.CreateHttpsClient();
            string customerEmail = await RegisterCustomer(client);
            string adminEmail = NewEmail();
            using var scope = application.Services.CreateScope();
            DatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();

            await initializer.CreateAdmin(adminEmail, TestPassword);
            await Assert.ThrowsAsync<InvalidOperationException>(() => initializer.CreateAdmin(customerEmail, TestPassword));

            UserManager<ApplicationUser> manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser administrator = (await manager.FindByEmailAsync(adminEmail))!;
            ApplicationUser customer = (await manager.FindByEmailAsync(customerEmail))!;
            Assert.True(await manager.IsInRoleAsync(administrator, ApplicationRoles.Admin));
            Assert.True(await manager.IsInRoleAsync(administrator, ApplicationRoles.Customer));
            Assert.False(await manager.IsInRoleAsync(customer, ApplicationRoles.Admin));
        }

        private static async Task<string> RegisterCustomer(HttpClient client)
        {
            string email = NewEmail();
            await RefreshCsrf(client);
            HttpResponseMessage response = await client.PostAsJsonAsync("/api/auth/register",
                new { email, password = TestPassword });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return email;
        }

        private static async Task RefreshCsrf(HttpClient client)
        {
            HttpResponseMessage response = await client.GetAsync("/api/auth/csrf");
            response.EnsureSuccessStatusCode();
            JsonElement payload = await response.Content.ReadFromJsonAsync<JsonElement>();
            client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
            client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", payload.GetProperty("token").GetString());
        }
    }
}
