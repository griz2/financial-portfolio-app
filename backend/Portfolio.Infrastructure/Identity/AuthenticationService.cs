using Microsoft.AspNetCore.Identity;
using Portfolio.Core.Authentication;
using Portfolio.Infrastructure.Data;

namespace Portfolio.Infrastructure.Identity
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly PortfolioDbContext _context;

        public AuthenticationService(UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager, PortfolioDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        public async Task<RegistrationResult> Register(string email, string password)
        {
            ApplicationUser user = new ApplicationUser()
            {
                UserName = email.Trim(),
                Email = email.Trim()
            };

            await using var transaction = await _context.Database.BeginTransactionAsync();
            IdentityResult result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                result = await _userManager.AddToRoleAsync(user, ApplicationRoles.Customer);
            }

            if (!result.Succeeded)
            {
                RegistrationResult failure = new RegistrationResult();

                foreach (IdentityError error in result.Errors)
                {
                    if (error.Code == "DuplicateEmail" || error.Code == "DuplicateUserName")
                    {
                        failure.Errors.Add("Registration could not be completed with these details.");
                    }
                    else
                    {
                        failure.Errors.Add(error.Description);
                    }
                }

                return failure;
            }

            await transaction.CommitAsync();
            return new RegistrationResult()
            {
                Succeeded = true
            };
        }

        public async Task<bool> Login(string email, string password)
        {
            ApplicationUser? user = await _userManager.FindByEmailAsync(email.Trim());

            if (user == null || !user.IsActive)
            {
                return false;
            }

            SignInResult result = await _signInManager.PasswordSignInAsync(user, password,
                isPersistent: false, lockoutOnFailure: true);
            return result.Succeeded;
        }

        public async Task Logout()
        {
            await _signInManager.SignOutAsync();
        }

        public async Task<UserDetails?> GetCurrentUser(string userId)
        {
            ApplicationUser? user = await _userManager.FindByIdAsync(userId);

            if (user == null || !user.IsActive)
            {
                return null;
            }

            IList<string> roles = await _userManager.GetRolesAsync(user);
            return new UserDetails()
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                Roles = roles.ToList()
            };
        }
    }
}
