using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImagePath",
                table: "Questions",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImagePath",
                table: "Questions");
        }
    }
}
