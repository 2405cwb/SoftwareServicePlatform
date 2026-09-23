using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientActivationCode_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientActivationCode");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientInstallation_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientInstallation");

            migrationBuilder.DropTable(
                name: "ClientUpdateCredentials");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClientInstallation",
                table: "ClientInstallation");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClientActivationCode",
                table: "ClientActivationCode");

            migrationBuilder.RenameTable(
                name: "ClientInstallation",
                newName: "ClientInstallations");

            migrationBuilder.RenameTable(
                name: "ClientActivationCode",
                newName: "ClientActivationCodes");

            migrationBuilder.RenameIndex(
                name: "IX_ClientInstallation_TokenHash",
                table: "ClientInstallations",
                newName: "IX_ClientInstallations_TokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_ClientInstallation_InstallationId",
                table: "ClientInstallations",
                newName: "IX_ClientInstallations_InstallationId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientInstallation_CustomerSoftwareId",
                table: "ClientInstallations",
                newName: "IX_ClientInstallations_CustomerSoftwareId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientActivationCode_CustomerSoftwareId",
                table: "ClientActivationCodes",
                newName: "IX_ClientActivationCodes_CustomerSoftwareId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientActivationCode_CodeHash",
                table: "ClientActivationCodes",
                newName: "IX_ClientActivationCodes_CodeHash");

            migrationBuilder.AddColumn<int>(
                name: "MaxDeviceCount",
                table: "CustomerSoftwares",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                table: "ClientInstallations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClientInstallations",
                table: "ClientInstallations",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClientActivationCodes",
                table: "ClientActivationCodes",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemEventLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Detail = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    UserName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TraceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemEventLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_CreatedAt",
                table: "SystemEventLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SystemEventLogs_Level_CreatedAt",
                table: "SystemEventLogs",
                columns: new[] { "Level", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ClientActivationCodes_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientActivationCodes",
                column: "CustomerSoftwareId",
                principalTable: "CustomerSoftwares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientInstallations_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientInstallations",
                column: "CustomerSoftwareId",
                principalTable: "CustomerSoftwares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientActivationCodes_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientActivationCodes");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientInstallations_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientInstallations");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "SystemEventLogs");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClientInstallations",
                table: "ClientInstallations");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ClientActivationCodes",
                table: "ClientActivationCodes");

            migrationBuilder.DropColumn(
                name: "MaxDeviceCount",
                table: "CustomerSoftwares");

            migrationBuilder.DropColumn(
                name: "Remark",
                table: "ClientInstallations");

            migrationBuilder.RenameTable(
                name: "ClientInstallations",
                newName: "ClientInstallation");

            migrationBuilder.RenameTable(
                name: "ClientActivationCodes",
                newName: "ClientActivationCode");

            migrationBuilder.RenameIndex(
                name: "IX_ClientInstallations_TokenHash",
                table: "ClientInstallation",
                newName: "IX_ClientInstallation_TokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_ClientInstallations_InstallationId",
                table: "ClientInstallation",
                newName: "IX_ClientInstallation_InstallationId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientInstallations_CustomerSoftwareId",
                table: "ClientInstallation",
                newName: "IX_ClientInstallation_CustomerSoftwareId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientActivationCodes_CustomerSoftwareId",
                table: "ClientActivationCode",
                newName: "IX_ClientActivationCode_CustomerSoftwareId");

            migrationBuilder.RenameIndex(
                name: "IX_ClientActivationCodes_CodeHash",
                table: "ClientActivationCode",
                newName: "IX_ClientActivationCode_CodeHash");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClientInstallation",
                table: "ClientInstallation",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ClientActivationCode",
                table: "ClientActivationCode",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "ClientUpdateCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerSoftwareId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenPrefix = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientUpdateCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientUpdateCredentials_CustomerSoftwares_CustomerSoftwareId",
                        column: x => x.CustomerSoftwareId,
                        principalTable: "CustomerSoftwares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientUpdateCredentials_CustomerSoftwareId",
                table: "ClientUpdateCredentials",
                column: "CustomerSoftwareId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientUpdateCredentials_TokenHash",
                table: "ClientUpdateCredentials",
                column: "TokenHash",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientActivationCode_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientActivationCode",
                column: "CustomerSoftwareId",
                principalTable: "CustomerSoftwares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ClientInstallation_CustomerSoftwares_CustomerSoftwareId",
                table: "ClientInstallation",
                column: "CustomerSoftwareId",
                principalTable: "CustomerSoftwares",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
