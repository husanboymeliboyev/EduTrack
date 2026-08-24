using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdjustmentReason",
                table: "StudentGrades",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsManualOverride",
                table: "StudentGrades",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "PreviousScore",
                table: "StudentGrades",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "StudentGrades",
                type: "TEXT",
                nullable: false,
                defaultValue: "System");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedByUserId",
                table: "StudentGrades",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdjustmentReason",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "IsManualOverride",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "PreviousScore",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "StudentGrades");

            migrationBuilder.DropColumn(
                name: "UpdatedByUserId",
                table: "StudentGrades");
        }
    }
}
