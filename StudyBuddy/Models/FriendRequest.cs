namespace StudyBuddy.Models;

public enum FriendRequestStatus
{
    Pending,
    Accepted,
    Rejected
}

public class FriendRequest
{
    public int FriendRequestId { get; set; }

    public string SenderId { get; set; } = string.Empty;
    public ApplicationUser Sender { get; set; } = null!;

    public string ReceiverId { get; set; } = string.Empty;
    public ApplicationUser Receiver { get; set; } = null!;

    public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
