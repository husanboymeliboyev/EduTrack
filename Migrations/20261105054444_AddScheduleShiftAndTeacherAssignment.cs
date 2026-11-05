using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduleShiftAndTeacherAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Schedules_GroupId",
                table: "Schedules");

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "Schedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Period",
                table: "Schedules",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Shift",
                table: "Schedules",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TeacherId",
                table: "Schedules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherId",
                table: "GroupSubjects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_GroupId_DayOfWeek_Shift_Period",
                table: "Schedules",
                columns: new[] { "GroupId", "DayOfWeek", "Shift", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_TeacherId_DayOfWeek_Shift_Period",
                table: "Schedules",
                columns: new[] { "TeacherId", "DayOfWeek", "Shift", "Period" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSubjects_GroupId_TeacherId",
                table: "GroupSubjects",
                columns: new[] { "GroupId", "TeacherId" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupSubjects_TeacherId",
                table: "GroupSubjects",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_GroupSubjects_AspNetUsers_TeacherId",
                table: "GroupSubjects",
                column: "TeacherId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Schedules_AspNetUsers_TeacherId",
                table: "Schedules",
                column: "TeacherId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GroupSubjects_AspNetUsers_TeacherId",
                table: "GroupSubjects");

            migrationBuilder.DropForeignKey(
                name: "FK_Schedules_AspNetUsers_TeacherId",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_GroupId_DayOfWeek_Shift_Period",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_Schedules_TeacherId_DayOfWeek_Shift_Period",
                table: "Schedules");

            migrationBuilder.DropIndex(
                name: "IX_GroupSubjects_GroupId_TeacherId",
                table: "GroupSubjects");

            migrationBuilder.DropIndex(
                name: "IX_GroupSubjects_TeacherId",
                table: "GroupSubjects");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Period",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "Shift",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "GroupSubjects");

            migrationBuilder.CreateIndex(
                name: "IX_Schedules_GroupId",
                table: "Schedules",
                column: "GroupId");
        }
    }
}
