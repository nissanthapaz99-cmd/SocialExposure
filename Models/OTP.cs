using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class OTP
    {
        public int Id { get; set; }

        [Required]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = string.Empty;

        public DateTime ExpiryTime { get; set; }

        public bool IsUsed { get; set; } = false;
    }
}