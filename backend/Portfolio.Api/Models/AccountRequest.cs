using System.ComponentModel.DataAnnotations;

namespace Portfolio.Api.Models
{
    public class AccountRequest
    {
        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;
    }
}
