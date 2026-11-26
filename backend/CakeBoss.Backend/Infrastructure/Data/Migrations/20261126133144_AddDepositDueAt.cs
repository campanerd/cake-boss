using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CakeBoss.Backend.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositDueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DepositDueAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositDueAt",
                table: "Orders");
        }
    }
}
