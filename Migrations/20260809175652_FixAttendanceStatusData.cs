using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class FixAttendanceStatusData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Oldingi migratsiyadagi xato tufayli davomat ma'lumotlari noto'g'ri
            // aylantirilgan edi. Bu — sinov ma'lumotlari bo'lgani uchun, ularni
            // tozalab, yangidan, to'g'ri Status qiymatlari bilan belgilashni
            // boshlaymiz.
            migrationBuilder.Sql("DELETE FROM Attendances");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
