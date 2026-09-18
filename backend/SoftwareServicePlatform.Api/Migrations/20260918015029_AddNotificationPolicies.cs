using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EventName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InAppEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RecipientStrategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DefaultLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationPolicyChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotificationPolicyId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MentionRecipient = table.Column<bool>(type: "boolean", nullable: false),
                    MentionAll = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPolicyChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationPolicyChannels_NotificationPolicies_Notificatio~",
                        column: x => x.NotificationPolicyId,
                        principalTable: "NotificationPolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPolicies_EventKey",
                table: "NotificationPolicies",
                column: "EventKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationPolicyChannels_NotificationPolicyId_Channel",
                table: "NotificationPolicyChannels",
                columns: new[] { "NotificationPolicyId", "Channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationPolicyChannels");

            migrationBuilder.DropTable(
                name: "NotificationPolicies");
        }
    }
}
