using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class EventStaff
    {
        public int Id { get; set; }

        [Required]
        public int EventId { get; set; }

        public Event? Event { get; set; }

        [Required]
        public int StaffId { get; set; }

        public User? Staff { get; set; }
    }
}