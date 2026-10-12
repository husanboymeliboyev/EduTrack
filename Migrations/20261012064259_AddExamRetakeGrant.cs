using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRetakeGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "ExamAttempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ExamRetakeGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    StudentId = table.Column<string>(type: "TEXT", nullable: false),
                    GrantedByTeacherId = table.Column<string>(type: "TEXT", nullable: false),
                    GrantedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    IsUsed = table.Column<bool>(type: "INTEGER", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    PreviousWasForceFinished = table.Column<bool>(type: "INTEGER", nullable: true),
                    PreviousViolationCount = table.Column<int>(type: "INTEGER", nullable: true),
                    PreviousLastViolationReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamRetakeGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExamRetakeGrants_AspNetUsers_StudentId",
                        column: x => x.StudentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExamRetakeGrants_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamRetakeGrants_ExamId_StudentId",
                table: "ExamRetakeGrants",
                columns: new[] { "ExamId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamRetakeGrants_StudentId",
                table: "ExamRetakeGrants",
                column: "StudentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExamRetakeGrants");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "ExamAttempts");
        }
    }
}
