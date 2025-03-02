using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FileUploader.Db.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUniquenessRuleForNamePlusExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileAssets_FullName",
                schema: "public",
                table: "FileAssets");

            migrationBuilder.DropColumn(
                name: "FullName",
                schema: "public",
                table: "FileAssets");

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_Name_Extension",
                schema: "public",
                table: "FileAssets",
                columns: new[] { "Name", "Extension" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FileAssets_Name_Extension",
                schema: "public",
                table: "FileAssets");

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                schema: "public",
                table: "FileAssets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_FileAssets_FullName",
                schema: "public",
                table: "FileAssets",
                column: "FullName",
                unique: true);
        }
    }
}
