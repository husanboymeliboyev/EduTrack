using EduTrack.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    // Migratsiya qo'lda yozilgani uchun atributlar Designer faylida emas, shu
    // yerda turadi. EF Core migratsiyani aynan shu atributlar orqali topadi.
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261119090000_AddSubmissionModeAndTextAnswer")]
    public partial class AddSubmissionModeAndTextAnswer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Topshiriq uchun javob turi: 0 = fayl yoki matn (eski xatti-harakat),
            // 1 = faqat fayl, 2 = faqat matn.
            migrationBuilder.AddColumn<int>(
                name: "SubmissionMode",
                table: "Assignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            // O'quvchining matnli javobi (matematika va formulalar uchun).
            migrationBuilder.AddColumn<string>(
                name: "TextAnswer",
                table: "Submissions",
                type: "TEXT",
                maxLength: 4000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionMode",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "TextAnswer",
                table: "Submissions");
        }
    }
}
