namespace Portfolio.Core.Authentication
{
    public interface IAuthenticationService
    {
        Task<RegistrationResult> Register(string email, string password);
        Task<bool> Login(string email, string password);
        Task Logout();
        Task<UserDetails?> GetCurrentUser(string userId);
    }
}
