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

    public DbSet<MessageReport> MessageReports { get; set; } = null!;

    public DbSet<ModerationAction> ModerationActions { get; set; } = null!;

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

        builder.Entity<ChatGroup>()
            .HasOne(g => g.CreatedBy)
            .WithMany()
            .HasForeignKey(g => g.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatGroupMember>()
            .HasIndex(m => new { m.ChatGroupId, m.UserId })
            .IsUnique();

        builder.Entity<ChatMessage>()
            .HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReport>()
           .HasOne(r => r.ChatMessage)
           .WithMany(m => m.Reports)
           .HasForeignKey(r => r.ChatMessageId)
           .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReport>()
            .HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReport>()
            .HasOne(r => r.ReviewedBy)
            .WithMany()
            .HasForeignKey(r => r.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MessageReport>()
            .HasIndex(r => new
            {
                r.ChatMessageId,
                r.ReporterId
            })
            .IsUnique();

        builder.Entity<MessageReport>()
            .HasIndex(r => new
            {
                r.Status,
                r.CreatedUtc
            });

        builder.Entity<ModerationAction>()
            .HasOne(a => a.MessageReport)
            .WithMany(r => r.Actions)
            .HasForeignKey(a => a.MessageReportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ModerationAction>()
            .HasOne(a => a.Admin)
            .WithMany()
            .HasForeignKey(a => a.AdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ChatMessage>()
            .HasOne(m => m.DeletedBy)
            .WithMany()
            .HasForeignKey(m => m.DeletedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}