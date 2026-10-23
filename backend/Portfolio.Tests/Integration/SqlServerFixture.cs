using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portfolio.Infrastructure.Data;

namespace Portfolio.Tests.Integration
{
    public class PortfolioApplication : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly string _environment;

        public PortfolioApplication(string connectionString, string environment = "Testing")
        {
            _connectionString = connectionString;
            _environment = environment;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environment);
            builder.ConfigureServices(services =>
                services.AddDataProtection().UseEphemeralDataProtectionProvider());
            builder.ConfigureAppConfiguration((context, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>()
                {
                    ["ConnectionStrings:PortfolioDatabase"] = _connectionString,
                    ["Logging:LogLevel:Default"] = "Warning"
                });
            });
        }

        public HttpClient CreateHttpsClient()
        {
            return CreateClient(new WebApplicationFactoryClientOptions()
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false,
                HandleCookies = true
            });
        }
    }

    public class SqlServerFixture : IAsyncLifetime
    {
        private readonly string _databaseName = "PortfolioTests_" + Guid.NewGuid().ToString("N");
        private PortfolioApplication? _application;
        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            string serverConnection = Environment.GetEnvironmentVariable("PORTFOLIO_TEST_SQL_CONNECTION")
                ?? "Server=localhost;Trusted_Connection=true;Encrypt=true;TrustServerCertificate=true";
            SqlConnectionStringBuilder connection = new SqlConnectionStringBuilder(serverConnection);
            connection.InitialCatalog = _databaseName;
            ConnectionString = connection.ConnectionString;

            _application = new PortfolioApplication(ConnectionString);
            using var scope = _application.Services.CreateScope();
            DatabaseInitializer initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
            await initializer.Initialize();
        }

        public PortfolioApplication CreateApplication()
        {
            return new PortfolioApplication(ConnectionString);
        }

        public PortfolioDbContext CreateContext()
        {
            DbContextOptions<PortfolioDbContext> options = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseSqlServer(ConnectionString).Options;
            return new PortfolioDbContext(options);
        }

        public async Task DisposeAsync()
        {
            if (_application == null)
            {
                return;
            }

            await _application.DisposeAsync();

            SqlConnectionStringBuilder connection = new SqlConnectionStringBuilder(ConnectionString);

            if (connection.InitialCatalog != _databaseName ||
                !_databaseName.StartsWith("PortfolioTests_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing to clean up an unexpected test database.");
            }

            await using PortfolioDbContext context = CreateContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}
