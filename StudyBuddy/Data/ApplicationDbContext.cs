using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Models;

namespace StudyBuddy.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Skill> Skills { get; set; } = null!;

    public DbSet<UserSkill> UserSkills { get; set; } = null!;

    public DbSet<FriendRequest> FriendRequests { get; set; } = null!;

    public DbSet<ChatGroup> ChatGroups { get; set; } = null!;

    public DbSet<ChatGroupMember> ChatGroupMembers { get; set; } = null!;

    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FriendRequest>()
            .HasOne(fr => fr.Sender)
            .WithMany()
            .HasForeignKey(fr => fr.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FriendRequest>()
            .HasOne(fr => fr.Receiver)
            .WithMany()
            .HasForeignKey(fr => fr.ReceiverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatGroupMember>()
            .HasIndex(m => new { m.ChatGroupId, m.UserId })
            .IsUnique();

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}