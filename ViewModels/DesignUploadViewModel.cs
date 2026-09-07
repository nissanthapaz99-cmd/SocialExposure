using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SocialExposure.Models;

namespace SocialExposure.ViewModels;

public sealed class DesignUploadViewModel
{
    [Required]
    [StringLength(120)]
    [Display(Name = "Design Name")]
    public string DesignName { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Select an event.")]
    [Display(Name = "Project or Event")]
    public int EventId { get; set; }

    [Required]
    [Display(Name = "Design File")]
    public IFormFile? File { get; set; }

    [StringLength(1000)]
    [Display(Name = "Version Notes")]
    public string? Description { get; set; }

    [Required]
    [StringLength(30)]
    public string Version { get; set; } = "v1.0";

    [ValidateNever]
    public IReadOnlyList<Event> Events { get; set; } = [];
}
