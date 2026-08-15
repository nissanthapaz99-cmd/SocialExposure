using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        public string? Type { get; set; }

        public string? Link { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public User? User { get; set; }
    }
}