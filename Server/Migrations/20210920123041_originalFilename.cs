using Microsoft.EntityFrameworkCore.Migrations;

namespace FileTransferazor.Server.Migrations
{
    public partial class originalFilename : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OriginalFileName",
                table: "FileStorageDatas",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OriginalFileName",
                table: "FileStorageDatas");
        }
    }
}
