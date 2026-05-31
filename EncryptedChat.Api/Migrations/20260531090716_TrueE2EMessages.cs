using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class TrueE2EMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM PinnedMessages;");
            migrationBuilder.Sql("DELETE FROM Attachments;");
            migrationBuilder.Sql("DELETE FROM Messages;");
            migrationBuilder.Sql("DELETE FROM Members;");
            migrationBuilder.Sql("DELETE FROM UserTeamPreferences;");
            migrationBuilder.Sql("DELETE FROM Teams;");

            migrationBuilder.DropColumn(
                name: "Secret",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "Secret",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<int>(
                name: "KeyGeneration",
                table: "Teams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Signature",
                table: "Messages",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<int>(
                name: "KeyGeneration",
                table: "Messages",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Signature",
                table: "Attachments",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<int>(
                name: "KeyGeneration",
                table: "Attachments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "EncryptedKeyBundle",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionPublicKey",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KeyBundleSalt",
                table: "AspNetUsers",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SigningPublicKey",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TeamKeyShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TeamId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MemberId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Generation = table.Column<int>(type: "int", nullable: false),
                    WrappedKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamKeyShares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamKeyShares_AspNetUsers_MemberId",
                        column: x => x.MemberId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamKeyShares_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamKeyShares_MemberId",
                table: "TeamKeyShares",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamKeyShares_TeamId_MemberId_Generation",
                table: "TeamKeyShares",
                columns: new[] { "TeamId", "MemberId", "Generation" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Down() restores the schema but cannot resurrect the deleted
            // PinnedMessages / Attachments / Messages / Members / Teams rows
            // that were wiped in Up().

            migrationBuilder.DropTable(
                name: "TeamKeyShares");

            migrationBuilder.DropColumn(
                name: "KeyGeneration",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "KeyGeneration",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "KeyGeneration",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "EncryptedKeyBundle",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EncryptionPublicKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "KeyBundleSalt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SigningPublicKey",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<string>(
                name: "Secret",
                table: "Teams",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "Signature",
                table: "Messages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Signature",
                table: "Attachments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(128)",
                oldMaxLength: 128);

            migrationBuilder.AddColumn<string>(
                name: "Secret",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
