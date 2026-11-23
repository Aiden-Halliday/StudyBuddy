using StudyBuddy.Models;

namespace StudyBuddy.Services
{
    public record ModerationResult(
        bool Succeeded,
        string Message
    );

    public interface IModerationService
    {
        Task<ModerationResult> IssueWarningAsync(
            int reportId,
            string adminId,
            string adminComment);

        Task<ModerationResult> DeleteMessageAsync(
            int reportId,
            string adminId,
            string adminComment);

        Task<ModerationResult> RestoreMessageAsync(
            int reportId,
            string adminId,
            string adminComment);

        Task<ModerationResult> SuspendUserAsync(
            int reportId,
            string adminId,
            string adminComment,
            DateTime expiresUtc);

        Task<ModerationResult> LiftSuspensionAsync(
            int reportId,
            string adminId,
            string adminComment);

        Task<ModerationResult> DismissReportAsync(
            int reportId,
            string adminId,
            string adminComment);
    }
}
