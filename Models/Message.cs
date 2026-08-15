using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public int SenderId { get; set; }

        [Required]
        public int ReceiverId { get; set; }

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime SentAt { get; set; } = DateTime.Now;

        public User? Sender { get; set; }

        public User? Receiver { get; set; }
    }
}