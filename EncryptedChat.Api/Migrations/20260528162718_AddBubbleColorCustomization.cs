using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class AddBubbleColorCustomization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwnBubbleColor",
                table: "Teams",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserTeamPreferences",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BubbleColor = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTeamPreferences", x => new { x.UserId, x.TeamId });
                    table.ForeignKey(
                        name: "FK_UserTeamPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTeamPreferences_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTeamPreferences_TeamId",
                table: "UserTeamPreferences",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTeamPreferences");

            migrationBuilder.DropColumn(
                name: "OwnBubbleColor",
                table: "Teams");
        }
    }
}
