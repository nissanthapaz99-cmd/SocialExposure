using Microsoft.EntityFrameworkCore;
using SocialExposure.Models;

namespace SocialExposure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<OTP> OTPs { get; set; }
    public DbSet<Project> Projects { get; set; }
    
}