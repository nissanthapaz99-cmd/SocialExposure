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
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<Event> Events { get; set; }
    public DbSet<Design> Designs { get; set; }

    // Staff assigned to events/projects
    public DbSet<EventStaff> EventStaff { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =========================================
        // MESSAGE - SENDER
        // =========================================

        modelBuilder.Entity<Message>()
            .HasOne(x => x.Sender)
            .WithMany()
            .HasForeignKey(x => x.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // =========================================
        // MESSAGE - RECEIVER
        // =========================================

        modelBuilder.Entity<Message>()
            .HasOne(x => x.Receiver)
            .WithMany()
            .HasForeignKey(x => x.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        // =========================================
        // EVENT - STAFF
        // =========================================

        modelBuilder.Entity<EventStaff>()
            .HasOne(x => x.Event)
            .WithMany(x => x.EventStaff)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Cascade);

        // =========================================
        // STAFF - EVENT
        // =========================================

        modelBuilder.Entity<EventStaff>()
            .HasOne(x => x.Staff)
            .WithMany()
            .HasForeignKey(x => x.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // =========================================
        // PREVENT DUPLICATE STAFF ASSIGNMENT
        // =========================================

        modelBuilder.Entity<EventStaff>()
            .HasIndex(x => new
            {
                x.EventId,
                x.StaffId
            })
            .IsUnique();
    }
}