using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Services
{
    public class ModerationService : IModerationService
    {
        private readonly ApplicationDbContext _context;

        public ModerationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task<ModerationResult> IssueWarningAsync(
            int reportId,
            string adminId,
            string adminComment)
        {
            return RecordActionAsync(
                reportId,
                adminId,
                adminComment,
                ModerationActionType.Warning,
                updateReport: (report, now) =>
                {
                    report.Status = MessageReportStatus.Resolved;
                });
        }

        public Task<ModerationResult> DeleteMessageAsync(
            int reportId,
            string adminId,
            string adminComment)
        {
            return RecordActionAsync(
                reportId,
                adminId,
                adminComment,
                ModerationActionType.DeleteMessage,
                validate: report =>
                    report.ChatMessage.IsDeleted
                        ? "This message has already been deleted."
                        : null,
                updateReport: (report, now) =>
                {
                    var message = report.ChatMessage;

                    message.IsDeleted = true;
                    message.DeletedById = adminId;
                    message.DeletedUtc = now;

                    report.Status = MessageReportStatus.Resolved;
                });
        }

        public Task<ModerationResult> RestoreMessageAsync(
            int reportId,
            string adminId,
            string adminComment)
        {
            return RecordActionAsync(
                reportId,
                adminId,
                adminComment,
                ModerationActionType.RestoreMessage,
                validate: report =>
                    !report.ChatMessage.IsDeleted
                        ? "This message has not been deleted."
                        : null,
                updateReport: (report, now) =>
                {
                    var message = report.ChatMessage;

                    message.IsDeleted = false;
                    message.DeletedById = null;
                    message.DeletedUtc = null;

                    report.Status = MessageReportStatus.Resolved;
                });
        }

        public Task<ModerationResult> SuspendUserAsync(
            int reportId,
            string adminId,
            string adminComment,
            DateTime expiresUtc)
        {
            var now = DateTime.UtcNow;

            if (expiresUtc <= now)
            {
                return Task.FromResult(new ModerationResult(
                    false,
                    "The suspension expiration must be in the future."));
            }

            return RecordActionAsync(
                reportId,
                adminId,
                adminComment,
                ModerationActionType.TemporarySuspension,
                expiresUtc: expiresUtc,
                validate: report =>
                    report.ChatMessage.Sender.SuspendedUntilUtc > now
                        ? "This user is already suspended."
                        : null,
                updateReport: (report, currentTime) =>
                {
                    report.ChatMessage.Sender.SuspendedUntilUtc =
                        expiresUtc;

                    report.Status = MessageReportStatus.Resolved;
                });
        }

        public Task<ModerationResult> LiftSuspensionAsync(
            int reportId,
            string adminId,
            string adminComment)
        {
            return RecordActionAsync(
                reportId,
                adminId,
                adminComment,
                ModerationActionType.LiftSuspension,
                validate: report =>
                    !report.ChatMessage.Sender.SuspendedUntilUtc.HasValue ||
                    report.ChatMessage.Sender.SuspendedUntilUtc <= DateTime.UtcNow
                        ? "This user does not have an active suspension."
                        : null,
                updateReport: (report, now) =>
                {
                    report.ChatMessage.Sender.SuspendedUntilUtc = null;

                    report.Status = MessageReportStatus.Resolved;
                });
        }

        public Task<ModerationResult> DismissReportAsync(
            int reportId,
            string adminId,
            string adminComment)
        {
            return RecordActionAsync(
                reportId,
                adminId,
                adminComment,
                ModerationActionType.Dismiss,
                updateReport: (report, now) =>
                {
                    report.Status = MessageReportStatus.Dismissed;
                });
        }

        private async Task<ModerationResult> RecordActionAsync(
            int reportId,
            string adminId,
            string adminComment,
            ModerationActionType actionType,
            DateTime? expiresUtc = null,
            Func<MessageReport, string?>? validate = null,
            Action<MessageReport, DateTime>? updateReport = null)
        {
            if (reportId <= 0)
            {
                return new ModerationResult(
                    false,
                    "The report ID is invalid.");
            }

            if (string.IsNullOrWhiteSpace(adminId))
            {
                return new ModerationResult(
                    false,
                    "An administrator ID is required.");
            }

            adminComment = adminComment?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(adminComment))
            {
                return new ModerationResult(
                    false,
                    "An administrator comment is required.");
            }

            if (adminComment.Length > 1000)
            {
                return new ModerationResult(
                    false,
                    "The administrator comment cannot exceed 1000 characters.");
            }

            var adminExists = await _context.Users
                .AnyAsync(user => user.Id == adminId);

            if (!adminExists)
            {
                return new ModerationResult(
                    false,
                    "The administrator account could not be found.");
            }

            var report = await _context.MessageReports
                .Include(r => r.ChatMessage)
                    .ThenInclude(m => m.Sender)
                .SingleOrDefaultAsync(r => r.MessageReportId == reportId);

            if (report == null)
            {
                return new ModerationResult(
                    false,
                    "The report could not be found.");
            }

            var validationError = validate?.Invoke(report);

            if (validationError != null)
            {
                return new ModerationResult(false, validationError);
            }

            var now = DateTime.UtcNow;

            updateReport?.Invoke(report, now);

            report.ReviewedById = adminId;
            report.ReviewedUtc = now;

            _context.ModerationActions.Add(new ModerationAction
            {
                MessageReportId = report.MessageReportId,
                AdminId = adminId,
                ActionType = actionType,
                AdminComment = adminComment,
                CreatedUtc = now,
                ExpiresUtc = expiresUtc
            });

            await _context.SaveChangesAsync();

            return new ModerationResult(
                true,
                "The moderation action was completed successfully.");
        }
    }
}
