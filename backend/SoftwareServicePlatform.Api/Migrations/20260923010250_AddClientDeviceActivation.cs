using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClientDeviceActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientActivationCode",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerSoftwareId = table.Column<int>(type: "integer", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UsedByInstallationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientActivationCode", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientActivationCode_CustomerSoftwares_CustomerSoftwareId",
                        column: x => x.CustomerSoftwareId,
                        principalTable: "CustomerSoftwares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClientInstallation",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CustomerSoftwareId = table.Column<int>(type: "integer", nullable: false),
                    InstallationId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeviceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TokenPrefix = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientInstallation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientInstallation_CustomerSoftwares_CustomerSoftwareId",
                        column: x => x.CustomerSoftwareId,
                        principalTable: "CustomerSoftwares",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivationCode_CodeHash",
                table: "ClientActivationCode",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientActivationCode_CustomerSoftwareId",
                table: "ClientActivationCode",
                column: "CustomerSoftwareId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInstallation_CustomerSoftwareId",
                table: "ClientInstallation",
                column: "CustomerSoftwareId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientInstallation_InstallationId",
                table: "ClientInstallation",
                column: "InstallationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClientInstallation_TokenHash",
                table: "ClientInstallation",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientActivationCode");

            migrationBuilder.DropTable(
                name: "ClientInstallation");
        }
    }
}
