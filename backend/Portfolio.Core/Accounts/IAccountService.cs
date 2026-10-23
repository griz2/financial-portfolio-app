namespace Portfolio.Core.Accounts
{
    public interface IAccountService
    {
        Task<List<AccountDetails>> GetAccounts(string ownerId);
        Task<AccountDetails?> GetAccount(string ownerId, int accountId);
        Task<AccountDetails> CreateAccount(string ownerId, string name);
        Task<AccountDetails?> RenameAccount(string ownerId, int accountId, string name);
    }
}
