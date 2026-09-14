using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SlaAppliedAt",
                table: "Tickets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaFirstResponseTargetMinutes",
                table: "Tickets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlaPriority",
                table: "Tickets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlaResolutionTargetMinutes",
                table: "Tickets",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaAppliedAt",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaFirstResponseTargetMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaPriority",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaResolutionTargetMinutes",
                table: "Tickets");
        }
    }
}
