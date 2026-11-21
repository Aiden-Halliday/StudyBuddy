using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyBuddy.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageReportingAndModeration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DeletedById",
                table: "ChatMessages",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedUtc",
                table: "ChatMessages",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ChatMessages",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MessageReports",
                columns: table => new
                {
                    MessageReportId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatMessageId = table.Column<int>(type: "int", nullable: false),
                    ReporterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedById = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ReviewedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageReports", x => x.MessageReportId);
                    table.ForeignKey(
                        name: "FK_MessageReports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageReports_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MessageReports_ChatMessages_ChatMessageId",
                        column: x => x.ChatMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "ChatMessageId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ModerationActions",
                columns: table => new
                {
                    ModerationActionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageReportId = table.Column<int>(type: "int", nullable: false),
                    AdminId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    AdminComment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModerationActions", x => x.ModerationActionId);
                    table.ForeignKey(
                        name: "FK_ModerationActions_AspNetUsers_AdminId",
                        column: x => x.AdminId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ModerationActions_MessageReports_MessageReportId",
                        column: x => x.MessageReportId,
                        principalTable: "MessageReports",
                        principalColumn: "MessageReportId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_DeletedById",
                table: "ChatMessages",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReports_ChatMessageId_ReporterId",
                table: "MessageReports",
                columns: new[] { "ChatMessageId", "ReporterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MessageReports_ReporterId",
                table: "MessageReports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReports_ReviewedById",
                table: "MessageReports",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_MessageReports_Status_CreatedUtc",
                table: "MessageReports",
                columns: new[] { "Status", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_AdminId",
                table: "ModerationActions",
                column: "AdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ModerationActions_MessageReportId",
                table: "ModerationActions",
                column: "MessageReportId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_AspNetUsers_DeletedById",
                table: "ChatMessages",
                column: "DeletedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_AspNetUsers_DeletedById",
                table: "ChatMessages");

            migrationBuilder.DropTable(
                name: "ModerationActions");

            migrationBuilder.DropTable(
                name: "MessageReports");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_DeletedById",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "DeletedUtc",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ChatMessages");
        }
    }
}
