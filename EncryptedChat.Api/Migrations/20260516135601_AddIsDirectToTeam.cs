using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDirectToTeam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDirect",
                table: "Teams",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsDirect",
                table: "Teams");
        }
    }
}
