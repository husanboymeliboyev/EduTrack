using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceCriteria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PerformanceCriterias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Key = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PerformanceCriterias", x => x.Id);
                });
            migrationBuilder.InsertData(
    table: "PerformanceCriterias",
    columns: new[] { "Id", "Key", "DisplayName", "Weight", "Description" },
    values: new object[,]
    {
        { 1, "OverallGrade", "Umumiy ball", 50.0, "Talabaning fanlar bo'yicha umumiy o'zlashtirish darajasi" },
        { 2, "Attendance", "Davomat", 25.0, "Talabaning darslarga qatnashish foizi" },
        { 3, "AssignmentCompletion", "Topshiriqlar", 15.0, "Topshirilgan va o'z vaqtida bajarilgan topshiriqlar ulushi" },
        { 4, "Trend", "Trend", 10.0, "Talaba ko'rsatkichlarining so'nggi davrdagi o'sish yoki pasayish tendensiyasi" }
    });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PerformanceCriterias");
        }
    }
}
