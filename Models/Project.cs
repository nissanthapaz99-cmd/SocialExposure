using System.ComponentModel.DataAnnotations;

namespace SocialExposure.Models
{
    public class Project
    {
        public int Id { get; set; }

        [Required]
        public string ProjectName { get; set; } = "";

        public string Description { get; set; } = "";

        public int ClientId { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public string Status { get; set; } = "Active";
    }
}