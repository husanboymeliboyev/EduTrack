using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddExamOpenClose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CloseAt",
                table: "Exams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
          name: "IsOpen",
          table: "Exams",
          type: "INTEGER",
          nullable: false,
          defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OpenAt",
                table: "Exams",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseAt",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "IsOpen",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "OpenAt",
                table: "Exams");
        }
    }
}
