using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.Models
{
    public class RegisterRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(128,
                MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(128)]
        public string Password { get; set; } = string.Empty;
    }
}
