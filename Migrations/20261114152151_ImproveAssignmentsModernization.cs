using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class ImproveAssignmentsModernization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GradedDate",
                table: "Submissions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLate",
                table: "Submissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StudentNote",
                table: "Submissions",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowLate",
                table: "Assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxPoints",
                table: "Assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GradedDate",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "IsLate",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "StudentNote",
                table: "Submissions");

            migrationBuilder.DropColumn(
                name: "AllowLate",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "MaxPoints",
                table: "Assignments");
        }
    }
}
