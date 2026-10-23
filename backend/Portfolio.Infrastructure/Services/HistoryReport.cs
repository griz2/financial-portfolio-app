using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Portfolio.Core.Management;

namespace Portfolio.Infrastructure.Services
{
    public class HistoryReport : IHistoryReport
    {
        private readonly string _connectionString;

        public HistoryReport(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("PortfolioDatabase")
                ?? throw new InvalidOperationException("The database connection is not configured.");
        }

        public async Task<List<TransactionDetails>> GetHistory(string ownerId, int accountId)
        {
            await using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using SqlCommand command = connection.CreateCommand();
            command.CommandText = """
                SELECT t.Id, t.AccountId, t.PortfolioId, i.Symbol, t.Type, t.Quantity,
                       t.UnitPrice, t.GrossAmount, t.RecordedAt
                FROM FinancialTransactions t
                INNER JOIN FinancialAccounts a ON t.AccountId = a.Id
                LEFT JOIN Investments i ON t.InvestmentId = i.Id
                WHERE a.OwnerId = @owner AND a.Id = @account
                ORDER BY t.RecordedAt DESC, t.Id DESC
                """;
            command.Parameters.Add("@owner", SqlDbType.NVarChar, 450).Value = ownerId;
            command.Parameters.Add("@account", SqlDbType.Int).Value = accountId;
            List<TransactionDetails> result = new List<TransactionDetails>();
            await using SqlDataReader reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                result.Add(new TransactionDetails()
                {
                    Id = reader.GetInt64(0),
                    AccountId = reader.GetInt32(1),
                    PortfolioId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                    Symbol = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Type = reader.GetString(4),
                    Quantity = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                    UnitPrice = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                    GrossAmount = reader.GetDecimal(7),
                    RecordedAt = DateTime.SpecifyKind(reader.GetDateTime(8), DateTimeKind.Utc)
                });
            }

            return result;
        }
    }
}
