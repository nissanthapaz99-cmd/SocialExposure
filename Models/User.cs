using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        // Client doesn't need a password
        public string? Password { get; set; }

        [Required]
        public string Role { get; set; } = "Client";

        public bool IsVerified { get; set; } = false;

        public bool IsActive { get; set; } = true;
    }
}