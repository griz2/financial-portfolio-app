namespace Portfolio.Core.Management
{
    public interface IHistoryReport
    {
        Task<List<TransactionDetails>> GetHistory(string ownerId, int accountId);
    }
}
