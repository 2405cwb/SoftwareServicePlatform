using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftwareVersionPublishStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublishStatus",
                table: "SoftwareVersions",
                type: "text",
                nullable: false,
                defaultValue: "");
            migrationBuilder.Sql(
                     """
                     UPDATE "SoftwareVersions"
                     SET "PublishStatus" =
                         CASE
                             WHEN "IsPublished" = TRUE
                                 THEN 'Published'
                             ELSE 'Draft'
                         END;
                     """
                );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PublishStatus",
                table: "SoftwareVersions");
        }
    }
}
