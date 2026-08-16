using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddQuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QuestionBankId",
                table: "Questions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "QuestionBankId",
                table: "Exams",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "QuestionBanks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    SubjectId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionBanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_QuestionBanks_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_QuestionBankId",
                table: "Questions",
                column: "QuestionBankId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_QuestionBankId",
                table: "Exams",
                column: "QuestionBankId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_GroupId",
                table: "QuestionBanks",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionBanks_SubjectId",
                table: "QuestionBanks",
                column: "SubjectId");

            // ===== MA'LUMOT MIGRATSIYASI (qo'lda qo'shildi) =====
            // Yuqorida QuestionBankId ustuni "defaultValue: 0" bilan qo'shildi — bu degani
            // mavjud barcha savol/imtihonlar hozir QuestionBankId = 0 ga ega, lekin QuestionBanks
            // jadvalida Id har doim 1 dan boshlanadi (autoincrement) — demak 0 hech qanday
            // haqiqiy bankka mos kelmaydi. Quyidagi 3 ta SQL blok buni tuzatadi:
            // har bir fan uchun (agar undan oldin savol/imtihoni bo'lgan bo'lsa) "Umumiy"
            // nomli standart bank yaratadi va eski yozuvlarni o'shanga bog'laydi.

            migrationBuilder.Sql(@"
                INSERT INTO QuestionBanks (Name, SubjectId, GroupId)
                SELECT DISTINCT 'Umumiy', s.Id, NULL
                FROM Subjects s
                WHERE EXISTS (SELECT 1 FROM Questions q WHERE q.SubjectId = s.Id)
                   OR EXISTS (SELECT 1 FROM Exams e WHERE e.SubjectId = s.Id);
            ");

            migrationBuilder.Sql(@"
                UPDATE Questions
                SET QuestionBankId = (
                    SELECT qb.Id FROM QuestionBanks qb
                    WHERE qb.SubjectId = Questions.SubjectId AND qb.Name = 'Umumiy'
                )
                WHERE QuestionBankId = 0;
            ");

            migrationBuilder.Sql(@"
                UPDATE Exams
                SET QuestionBankId = (
                    SELECT qb.Id FROM QuestionBanks qb
                    WHERE qb.SubjectId = Exams.SubjectId AND qb.Name = 'Umumiy'
                )
                WHERE QuestionBankId = 0;
            ");
            // ===== MA'LUMOT MIGRATSIYASI TUGADI =====

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_QuestionBanks_QuestionBankId",
                table: "Exams",
                column: "QuestionBankId",
                principalTable: "QuestionBanks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_QuestionBanks_QuestionBankId",
                table: "Questions",
                column: "QuestionBankId",
                principalTable: "QuestionBanks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_QuestionBanks_QuestionBankId",
                table: "Exams");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_QuestionBanks_QuestionBankId",
                table: "Questions");

            migrationBuilder.DropTable(
                name: "QuestionBanks");

            migrationBuilder.DropIndex(
                name: "IX_Questions_QuestionBankId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Exams_QuestionBankId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "QuestionBankId",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "QuestionBankId",
                table: "Exams");
        }
    }
}