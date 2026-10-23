using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Portfolio.Core.Authentication;
using Portfolio.Infrastructure.Identity;
using System.ComponentModel.DataAnnotations;

namespace Portfolio.Infrastructure.Data
{
    public class DatabaseInitializer
    {
        private readonly PortfolioDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public DatabaseInitializer(PortfolioDbContext context, RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task Initialize()
        {
            await _context.Database.MigrateAsync();

            foreach (string role in new[] { ApplicationRoles.Customer, ApplicationRoles.Admin })
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    CheckResult(await _roleManager.CreateAsync(new IdentityRole(role)));
                }
            }
        }

        public async Task CreateAdmin(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || email.Trim().Length > 256 ||
                !new EmailAddressAttribute().IsValid(email.Trim()))
            {
                throw new InvalidOperationException("Enter a valid administrator email with at most 256 characters.");
            }

            if (string.IsNullOrEmpty(password) || password.Length > 128)
            {
                throw new InvalidOperationException("Administrator passwords must contain at most 128 characters.");
            }

            if (await _userManager.FindByEmailAsync(email.Trim()) != null)
            {
                throw new InvalidOperationException("Administrator creation requires a new email; existing users are never promoted automatically.");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            ApplicationUser user = new ApplicationUser()
            {
                UserName = email.Trim(),
                Email = email.Trim()
            };

            CheckResult(await _userManager.CreateAsync(user, password));
            CheckResult(await _userManager.AddToRolesAsync(user,
                new[] { ApplicationRoles.Customer, ApplicationRoles.Admin }));
            await transaction.CommitAsync();
        }

        private static void CheckResult(IdentityResult result)
        {
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            }
        }
    }
}
