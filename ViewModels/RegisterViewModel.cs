using System.ComponentModel.DataAnnotations;

namespace SocialExposure.ViewModels
{
    public class RegisterViewModel
    {
        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public bool AcceptTerms { get; set; }
    }
}