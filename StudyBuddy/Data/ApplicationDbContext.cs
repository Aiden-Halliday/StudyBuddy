
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Models;

namespace StudyBuddy.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Skill> Skills { get; set; } = null!;
    public DbSet<UserSkill> UserSkills { get; set; } = null!;
    public DbSet<FriendRequest> FriendRequests { get; set; } = null!;
    public DbSet<ChatGroup> ChatGroups { get; set; } = null!;
    public DbSet<ChatGroupMember> ChatGroupMembers { get; set; } = null!;
    public DbSet<ChatMessage> ChatMessages { get; set; } = null!;
    public DbSet<ChatAttachment> ChatAttachments { get; set; } = null!;

    public DbSet<UserReputationVote> UserReputationVotes { get; set; } = null!;

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

        // Reputation system configuration
        builder.Entity<UserReputationVote>(entity =>
        {
            // A student can only vote once on another student.
            entity.HasIndex(v => new { v.VoterId, v.TargetUserId })
                .IsUnique();

            entity.HasOne(v => v.Voter)
                .WithMany()
                .HasForeignKey(v => v.VoterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(v => v.TargetUser)
                .WithMany()
                .HasForeignKey(v => v.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Prevent self-voting at database level.
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_UserReputationVotes_NoSelfVoting",
                "[VoterId] <> [TargetUserId]"));
        });

        builder.Entity<ChatAttachment>()
            .HasOne(a => a.ChatMessage)
            .WithMany(m => m.Attachments)
            .HasForeignKey(a => a.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChatAttachment>()
            .HasIndex(a => a.StorageKey)
            .IsUnique();
    }
}
