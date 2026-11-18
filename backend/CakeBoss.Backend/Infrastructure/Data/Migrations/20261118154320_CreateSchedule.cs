using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CakeBoss.Backend.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CreateSchedule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OpeningHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpeningHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduleSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    SlotMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinLeadDays = table.Column<int>(type: "integer", nullable: false),
                    MaxKgPerDay = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduleSettings", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "OpeningHours",
                columns: new[] { "Id", "ClosesAt", "DayOfWeek", "OpensAt" },
                values: new object[,]
                {
                    { 1, new TimeOnly(20, 0, 0), 2, new TimeOnly(14, 0, 0) },
                    { 2, new TimeOnly(20, 0, 0), 3, new TimeOnly(14, 0, 0) },
                    { 3, new TimeOnly(20, 0, 0), 4, new TimeOnly(14, 0, 0) },
                    { 4, new TimeOnly(20, 0, 0), 5, new TimeOnly(14, 0, 0) },
                    { 5, new TimeOnly(18, 0, 0), 6, new TimeOnly(8, 0, 0) }
                });

            migrationBuilder.InsertData(
                table: "ScheduleSettings",
                columns: new[] { "Id", "MaxKgPerDay", "MinLeadDays", "SlotMinutes" },
                values: new object[] { 1, 20m, 3, 60 });

            migrationBuilder.CreateIndex(
                name: "IX_OpeningHours_DayOfWeek",
                table: "OpeningHours",
                column: "DayOfWeek",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScheduleBlocks_StartDate_EndDate",
                table: "ScheduleBlocks",
                columns: new[] { "StartDate", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OpeningHours");

            migrationBuilder.DropTable(
                name: "ScheduleBlocks");

            migrationBuilder.DropTable(
                name: "ScheduleSettings");
        }
    }
}
