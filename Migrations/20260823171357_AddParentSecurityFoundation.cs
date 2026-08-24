using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduTrack.Migrations
{
    /// <inheritdoc />
    public partial class AddParentSecurityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradingScales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ExamId = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedDate = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingScales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingScales_Exams_ExamId",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ParentStudentLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ParentUserId = table.Column<string>(type: "TEXT", nullable: false),
                    StudentUserId = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParentStudentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParentStudentLinks_AspNetUsers_ParentUserId",
                        column: x => x.ParentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParentStudentLinks_AspNetUsers_StudentUserId",
                        column: x => x.StudentUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingScaleVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GradingScaleId = table.Column<int>(type: "INTEGER", nullable: false),
                    VersionNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    IsPublished = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublishedDate = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingScaleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingScaleVersions_GradingScales_GradingScaleId",
                        column: x => x.GradingScaleId,
                        principalTable: "GradingScales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GradingScaleBands",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    GradingScaleVersionId = table.Column<int>(type: "INTEGER", nullable: false),
                    MinimumScore = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    Color = table.Column<string>(type: "TEXT", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingScaleBands", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingScaleBands_GradingScaleVersions_GradingScaleVersionId",
                        column: x => x.GradingScaleVersionId,
                        principalTable: "GradingScaleVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradingScaleBands_GradingScaleVersionId_DisplayOrder",
                table: "GradingScaleBands",
                columns: new[] { "GradingScaleVersionId", "DisplayOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradingScales_ExamId_Name",
                table: "GradingScales",
                columns: new[] { "ExamId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GradingScaleVersions_GradingScaleId_VersionNumber",
                table: "GradingScaleVersions",
                columns: new[] { "GradingScaleId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentLinks_ParentUserId_StudentUserId",
                table: "ParentStudentLinks",
                columns: new[] { "ParentUserId", "StudentUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParentStudentLinks_StudentUserId",
                table: "ParentStudentLinks",
                column: "StudentUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradingScaleBands");

            migrationBuilder.DropTable(
                name: "ParentStudentLinks");

            migrationBuilder.DropTable(
                name: "GradingScaleVersions");

            migrationBuilder.DropTable(
                name: "GradingScales");
        }
    }
}
