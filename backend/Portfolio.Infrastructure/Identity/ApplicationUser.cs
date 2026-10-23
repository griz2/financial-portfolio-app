using Microsoft.AspNetCore.Identity;

namespace Portfolio.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser
    {
        public bool IsActive { get; set; } = true;
    }
}
