using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExtendDownloadRecordsForAutoUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DownloadType",
                table: "DownloadRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "DownloadRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FileCount",
                table: "DownloadRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FromVersion",
                table: "DownloadRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "DownloadRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ToVersion",
                table: "DownloadRecords",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
    """
    UPDATE "DownloadRecords"
    SET
        "DownloadType" = 'ManualPackage',
        "Status" = 'Success',
        "FileCount" = 1,
        "ToVersion" = "Version"
    WHERE
        "DownloadType" = ''
        AND "Status" = '';
    """
);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DownloadType",
                table: "DownloadRecords");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "DownloadRecords");

            migrationBuilder.DropColumn(
                name: "FileCount",
                table: "DownloadRecords");

            migrationBuilder.DropColumn(
                name: "FromVersion",
                table: "DownloadRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "DownloadRecords");

            migrationBuilder.DropColumn(
                name: "ToVersion",
                table: "DownloadRecords");
        }
    }
}
