using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class LinkSessionToRefreshToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CurrentRefreshTokenId",
                table: "Sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CurrentRefreshTokenId",
                table: "Sessions",
                column: "CurrentRefreshTokenId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sessions_RefreshTokens_CurrentRefreshTokenId",
                table: "Sessions",
                column: "CurrentRefreshTokenId",
                principalTable: "RefreshTokens",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sessions_RefreshTokens_CurrentRefreshTokenId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_CurrentRefreshTokenId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "CurrentRefreshTokenId",
                table: "Sessions");
        }
    }
}
