using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileTransferazor.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddContentTypeToFileStorageData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "FileStorageDatas",
                type: "text",
                nullable: false,
                defaultValue: "application/octet-stream");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "FileStorageDatas");
        }
    }
}
