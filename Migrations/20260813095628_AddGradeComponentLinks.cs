using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddGradeComponentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignmentId",
                table: "GradeComponents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamId",
                table: "GradeComponents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_AssignmentId",
                table: "GradeComponents",
                column: "AssignmentId",
                unique: true,
                filter: "AssignmentId IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GradeComponents_ExamId",
                table: "GradeComponents",
                column: "ExamId",
                unique: true,
                filter: "ExamId IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GradeComponent_SingleLink",
                table: "GradeComponents",
                sql: "AssignmentId IS NULL OR ExamId IS NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_GradeComponents_Assignments_AssignmentId",
                table: "GradeComponents",
                column: "AssignmentId",
                principalTable: "Assignments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GradeComponents_Exams_ExamId",
                table: "GradeComponents",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GradeComponents_Assignments_AssignmentId",
                table: "GradeComponents");

            migrationBuilder.DropForeignKey(
                name: "FK_GradeComponents_Exams_ExamId",
                table: "GradeComponents");

            migrationBuilder.DropIndex(
                name: "IX_GradeComponents_AssignmentId",
                table: "GradeComponents");

            migrationBuilder.DropIndex(
                name: "IX_GradeComponents_ExamId",
                table: "GradeComponents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_GradeComponent_SingleLink",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "GradeComponents");

            migrationBuilder.DropColumn(
                name: "ExamId",
                table: "GradeComponents");
        }
    }
}
