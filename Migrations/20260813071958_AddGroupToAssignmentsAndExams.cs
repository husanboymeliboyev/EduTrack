using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupToAssignmentsAndExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Exams",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Assignments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_GroupId",
                table: "Exams",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_GroupId",
                table: "Assignments",
                column: "GroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assignments_Groups_GroupId",
                table: "Assignments",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Groups_GroupId",
                table: "Exams",
                column: "GroupId",
                principalTable: "Groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assignments_Groups_GroupId",
                table: "Assignments");

            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Groups_GroupId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_GroupId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_GroupId",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Assignments");
        }
    }
}
