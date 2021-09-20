using Microsoft.EntityFrameworkCore.Migrations;

namespace FileTransferazor.Server.Migrations
{
    public partial class FileData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileSendDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SenderEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReceiverEmail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileSendDatas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileStorageDatas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileSendDataId = table.Column<int>(type: "int", nullable: false),
                    FileUri = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileStorageDatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileStorageDatas_FileSendDatas_FileSendDataId",
                        column: x => x.FileSendDataId,
                        principalTable: "FileSendDatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileStorageDatas_FileSendDataId",
                table: "FileStorageDatas",
                column: "FileSendDataId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileStorageDatas");

            migrationBuilder.DropTable(
                name: "FileSendDatas");
        }
    }
}
