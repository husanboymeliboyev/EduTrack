using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddExamRetakeWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "ExamResults",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "AttemptNumber",
                table: "ExamAttempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<bool>(
                name: "PendingRetake",
                table: "ExamAttempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RetakeGrantedAtUtc",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RetakeGrantedByUserId",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "AttemptNumber",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "PendingRetake",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "RetakeGrantedAtUtc",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "RetakeGrantedByUserId",
                table: "ExamAttempts");
        }
    }
}
