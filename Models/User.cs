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

        [StringLength(150)]
        public string? CompanyName { get; set; }

        [Phone]
        [StringLength(30)]
        public string? PhoneNumber { get; set; }

        [StringLength(100)]
        public string? JobTitle { get; set; }

        [StringLength(500)]
        public string? AccessReason { get; set; }

        [StringLength(20)]
        public string? PreferredContactMethod { get; set; } = "Email";

        public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? TermsAcceptedAt { get; set; }

        public DateTime? PrivacyAcceptedAt { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public int? ApprovedByUserId { get; set; }

        // Client doesn't need a password
        public string? Password { get; set; }

        [Required]
        public string Role { get; set; } = "Client";

        public bool IsVerified { get; set; } = false;

        public bool IsActive { get; set; } = true;

        // Self-registered clients must be approved by an administrator.
        // Existing and administrator-created accounts default to approved.
        public bool IsApproved { get; set; } = true;
    }
}
