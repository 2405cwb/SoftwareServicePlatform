using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SoftwareServicePlatform.Api.Migrations
{
    /// <inheritdoc />
    public partial class addmigrationAddSoftwareVersionPackageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PackageFileName",
                table: "SoftwareVersions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "PackageFileSize",
                table: "SoftwareVersions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "PackageRelativePath",
                table: "SoftwareVersions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PackageSha256",
                table: "SoftwareVersions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PackageUploadedAt",
                table: "SoftwareVersions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PackageFileName",
                table: "SoftwareVersions");

            migrationBuilder.DropColumn(
                name: "PackageFileSize",
                table: "SoftwareVersions");

            migrationBuilder.DropColumn(
                name: "PackageRelativePath",
                table: "SoftwareVersions");

            migrationBuilder.DropColumn(
                name: "PackageSha256",
                table: "SoftwareVersions");

            migrationBuilder.DropColumn(
                name: "PackageUploadedAt",
                table: "SoftwareVersions");
        }
    }
}
