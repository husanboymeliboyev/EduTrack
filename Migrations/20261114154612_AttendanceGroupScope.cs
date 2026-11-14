using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceGroupScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_Date_StudentId_SubjectId",
                table: "Attendances");

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Attendances",
                type: "INTEGER",
                nullable: true);

            // Eski yozuvlarni to'g'ri guruhga bog'lash (1-smena / 2-smena
            // aralashmasligi uchun). Ustuvorlik tartibi:
            //   1) Fan bo'yicha aniq biriktirilgan guruh (StudentGroupSubjects);
            //   2) Shu sanada shu fan bo'yicha DARS BO'LGAN yagona guruh —
            //      bir o'quvchi bir vaqtda ikki guruhda bo'lsa (masalan, ertalab
            //      sinf + kechqurun kurs), dars jadvali orqali aniq ajratamiz;
            //   3) Shu sanadagi eng so'nggi aktiv yo'nalish (Stream) a'zolik;
            //   4) Legacy AspNetUsers.GroupId.
            migrationBuilder.Sql(@"
UPDATE Attendances
SET GroupId = COALESCE(
    (SELECT sgs.GroupId
       FROM StudentGroupSubjects sgs
      WHERE sgs.StudentId = Attendances.StudentId
        AND sgs.SubjectId = Attendances.SubjectId
        AND sgs.StartDate <= Attendances.Date
        AND (sgs.EndDate IS NULL OR sgs.EndDate > Attendances.Date)
      ORDER BY sgs.StartDate DESC
      LIMIT 1),
    (SELECT CASE WHEN COUNT(DISTINCT sch.GroupId) = 1
                 THEN MIN(sch.GroupId) ELSE NULL END
       FROM Schedules sch
      WHERE sch.SubjectId = Attendances.SubjectId
        AND sch.DayOfWeek = CAST(strftime('%w', Attendances.Date) AS INTEGER)
        AND sch.GroupId IN (SELECT m.GroupId
                              FROM GroupMemberships m
                             WHERE m.StudentId = Attendances.StudentId
                               AND m.StartDate <= Attendances.Date
                               AND (m.EndDate IS NULL OR m.EndDate > Attendances.Date))),
    (SELECT m2.GroupId
       FROM GroupMemberships m2
       JOIN Groups g ON g.Id = m2.GroupId
      WHERE m2.StudentId = Attendances.StudentId
        AND g.Type = 0
        AND m2.StartDate <= Attendances.Date
        AND (m2.EndDate IS NULL OR m2.EndDate > Attendances.Date)
      ORDER BY m2.StartDate DESC
      LIMIT 1),
    (SELECT u.GroupId FROM AspNetUsers u WHERE u.Id = Attendances.StudentId)
);");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_Date_StudentId_SubjectId_GroupId",
                table: "Attendances",
                columns: new[] { "Date", "StudentId", "SubjectId", "GroupId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Attendances_Date_StudentId_SubjectId_GroupId",
                table: "Attendances");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Attendances");

            migrationBuilder.CreateIndex(
                name: "IX_Attendances_Date_StudentId_SubjectId",
                table: "Attendances",
                columns: new[] { "Date", "StudentId", "SubjectId" },
                unique: true);
        }
    }
}
