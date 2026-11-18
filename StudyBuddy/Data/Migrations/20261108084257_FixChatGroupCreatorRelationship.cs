using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyBuddy.Migrations
{
    /// <inheritdoc />
    public partial class FixChatGroupCreatorRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatGroups_AspNetUsers_CreatedById",
                table: "ChatGroups");

            migrationBuilder.DropIndex(
                name: "IX_ChatGroups_CreatedById",
                table: "ChatGroups");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "ChatGroups");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "ChatGroups",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_CreatedByUserId",
                table: "ChatGroups",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatGroups_AspNetUsers_CreatedByUserId",
                table: "ChatGroups",
                column: "CreatedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatGroups_AspNetUsers_CreatedByUserId",
                table: "ChatGroups");

            migrationBuilder.DropIndex(
                name: "IX_ChatGroups_CreatedByUserId",
                table: "ChatGroups");

            migrationBuilder.AlterColumn<string>(
                name: "CreatedByUserId",
                table: "ChatGroups",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "ChatGroups",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatGroups_CreatedById",
                table: "ChatGroups",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatGroups_AspNetUsers_CreatedById",
                table: "ChatGroups",
                column: "CreatedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
