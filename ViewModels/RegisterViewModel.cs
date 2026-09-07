using System.ComponentModel.DataAnnotations;

namespace SocialExposure.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Enter a valid full name.")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email address is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(254, ErrorMessage = "Email address is too long.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Company or organisation name is required.")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "Enter a valid company or organisation name.")]
        public string CompanyName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [Phone(ErrorMessage = "Enter a valid phone number.")]
        [StringLength(30, MinimumLength = 7, ErrorMessage = "Enter a valid phone number.")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Job title is too long.")]
        public string? JobTitle { get; set; }

        [Required(ErrorMessage = "Tell us why you need access.")]
        [StringLength(500, MinimumLength = 10, ErrorMessage = "Enter between 10 and 500 characters.")]
        public string AccessReason { get; set; } = string.Empty;

        [Required]
        [RegularExpression("^(Email|Phone)$", ErrorMessage = "Select email or phone as your preferred contact method.")]
        public string PreferredContactMethod { get; set; } = "Email";

        [Range(typeof(bool), "true", "true",
            ErrorMessage = "You must agree to the Terms & Conditions to sign up.")]
        public bool AcceptTerms { get; set; }

        [Range(typeof(bool), "true", "true",
            ErrorMessage = "You must agree to the Privacy Policy to sign up.")]
        public bool AcceptPrivacy { get; set; }
    }
}
