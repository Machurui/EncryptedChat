using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamGlyphAndColor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Teams",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "oklch(0.65 0.16 165)");

            migrationBuilder.AddColumn<string>(
                name: "Glyph",
                table: "Teams",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "◆");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Color",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Glyph",
                table: "Teams");
        }
    }
}
