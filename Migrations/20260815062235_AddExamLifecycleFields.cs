using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddExamLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Exams",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsApproved",
                table: "ExamResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsApproved",
                table: "ExamResults");
        }
    }
}
