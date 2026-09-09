using System;
using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class Event
    {
        public int Id { get; set; }

        [Required]
        public string EventName { get; set; } = string.Empty;

        [Required]
        public string ClientName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string ClientEmail { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime Deadline { get; set; }

        public string Status { get; set; } = "Pending";
    }
}