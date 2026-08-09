using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAttendanceToStatusEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsPresent",
                table: "Attendances",
                newName: "Status");

            // Eski IsPresent: true(1) = bor edi, false(0) = yo'q edi
            // Yangi Status enum: Keldi = 0, Kelmadi = 1
            // Shuning uchun qiymatlarni teskarisiga aylantiramiz
            migrationBuilder.Sql("UPDATE Attendances SET Status = CASE WHEN Status = 1 THEN 1 ELSE 2 END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Orqaga qaytishda ham xuddi shunday teskari aylantiramiz
            migrationBuilder.Sql("UPDATE Attendances SET Status = CASE WHEN Status = 1 THEN 1 ELSE 0 END");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "Attendances",
                newName: "IsPresent");
        }
    }
}