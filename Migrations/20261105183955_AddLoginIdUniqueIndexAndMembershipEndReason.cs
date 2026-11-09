using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginIdUniqueIndexAndMembershipEndReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EndReason",
                table: "GroupMemberships",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupMemberships_StudentId_IsActive",
                table: "GroupMemberships",
                columns: new[] { "StudentId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_LoginId",
                table: "AspNetUsers",
                column: "LoginId",
                unique: true,
                filter: "LoginId IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_GroupMemberships_StudentId_IsActive",
                table: "GroupMemberships");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_LoginId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EndReason",
                table: "GroupMemberships");
        }
    }
}
