using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberUrlToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add column as nullable to allow backfill
            migrationBuilder.AddColumn<string>(
                name: "UrlToken",
                table: "Members",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            // 2. Backfill existing rows with random 10-char tokens.
            // Uses NEWID() to ensure uniqueness across existing rows.
            // The C# TokenGenerator generates from a 53-char alphabet at runtime;
            // here we use uppercase hex (truncated from NEWID) for SQL simplicity.
            migrationBuilder.Sql(@"
                UPDATE [Members]
                SET [UrlToken] = LEFT(REPLACE(CONVERT(VARCHAR(36), NEWID()), '-', ''), 10)
                WHERE [UrlToken] IS NULL;
            ");

            // 3. Enforce NOT NULL
            migrationBuilder.AlterColumn<string>(
                name: "UrlToken",
                table: "Members",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(16)",
                oldMaxLength: 16,
                oldNullable: true);

            // 4. Add unique index
            migrationBuilder.CreateIndex(
                name: "IX_Members_UrlToken",
                table: "Members",
                column: "UrlToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Members_UrlToken",
                table: "Members");

            migrationBuilder.DropColumn(
                name: "UrlToken",
                table: "Members");
        }
    }
}
