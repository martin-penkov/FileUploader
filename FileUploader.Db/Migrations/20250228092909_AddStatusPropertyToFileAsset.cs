using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileUploader.Db.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusPropertyToFileAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "public",
                table: "FileAssets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Status",
                schema: "public",
                table: "FileAssets");
        }
    }
}
