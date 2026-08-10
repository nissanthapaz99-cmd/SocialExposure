using System.ComponentModel.DataAnnotations;

namespace SocialExposure.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}