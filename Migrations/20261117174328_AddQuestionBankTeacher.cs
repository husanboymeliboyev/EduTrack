using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBankTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestionBanks_SubjectId",
                table: "QuestionBanks");

            migrationBuilder.AddColumn<string>(
                name: "TeacherId",
                table: "QuestionBanks",
                type: "TEXT",
                nullable: true);

            // Eski banklarda TeacherId yo'q edi. Ular bir fanga biriktirilgan
            // bir nechta o'qituvchi orasida "hammaga ko'rinadigan" bo'lib
            // qolmasligi uchun fan egasiga (Subjects.TeacherId) biriktiramiz.
            // Fan egasi belgilanmagan bo'lsa TeacherId null qoladi — bunday
            // banklar QuestionBankAccess fallback qoidasi orqali faqat shu fan
            // o'qituvchilariga ko'rinadi.
            migrationBuilder.Sql(@"
                UPDATE QuestionBanks
                SET TeacherId = (
                    SELECT Subjects.TeacherId
                    FROM Subjects
                    WHERE Subjects.Id = QuestionBanks.SubjectId
                )
                WHERE TeacherId IS NULL;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_SubjectId_TeacherId",
                table: "QuestionBanks",
                columns: new[] { "SubjectId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_TeacherId",
                table: "QuestionBanks",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionBanks_AspNetUsers_TeacherId",
                table: "QuestionBanks",
                column: "TeacherId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionBanks_AspNetUsers_TeacherId",
                table: "QuestionBanks");

            migrationBuilder.DropIndex(
                name: "IX_QuestionBanks_SubjectId_TeacherId",
                table: "QuestionBanks");

            migrationBuilder.DropIndex(
                name: "IX_QuestionBanks_TeacherId",
                table: "QuestionBanks");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "QuestionBanks");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_SubjectId",
                table: "QuestionBanks",
                column: "SubjectId");
        }
    }
}
