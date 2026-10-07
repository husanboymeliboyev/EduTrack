using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddExamAttemptLiveMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ForceFinishedAtUtc",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ForceFinishedById",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsForceFinished",
                table: "ExamAttempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastViolationAtUtc",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastViolationReason",
                table: "ExamAttempts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ViolationCount",
                table: "ExamAttempts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForceFinishedAtUtc",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "ForceFinishedById",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "IsForceFinished",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "LastViolationAtUtc",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "LastViolationReason",
                table: "ExamAttempts");

            migrationBuilder.DropColumn(
                name: "ViolationCount",
                table: "ExamAttempts");
        }
    }
}
