using System;
using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class Design
    {
        public int Id { get; set; }

        [Required]
        public string FileName { get; set; } = string.Empty;

        public string? FilePath { get; set; }

        public string? Description { get; set; }

        public string Version { get; set; } = "v1.0";

        public DateTime UploadedAt { get; set; } = DateTime.Now;

        // The event this design belongs to
        [Required]
        public int EventId { get; set; }

        public Event? Event { get; set; }

        // The client who can see this design
        [Required]
        public int ClientId { get; set; }

        public User? Client { get; set; }

        // Review status
        public string Status { get; set; } = "Pending Review";
    }
}