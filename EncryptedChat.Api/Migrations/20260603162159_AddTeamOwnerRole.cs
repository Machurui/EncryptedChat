using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EncryptedChat.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamOwnerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No schema change — Member.Role is already a string column wide
            // enough for "Owner". This migration only backfills the new role
            // for existing teams so the new permission rules don't lock out
            // every team that was created before Owner existed.
            //
            // Heuristic: oldest Admin (by CreatedAt) per team becomes Owner.
            // Direct messages (IsDirect=1) are skipped — they don't need an
            // Owner concept.
            migrationBuilder.Sql(@"
                WITH FirstAdmins AS (
                    SELECT m.Id,
                           ROW_NUMBER() OVER (PARTITION BY m.TeamId ORDER BY m.CreatedAt, m.Id) AS rn
                    FROM Members m
                    INNER JOIN Teams t ON t.Id = m.TeamId
                    WHERE m.Role = 'Admin'
                      AND t.IsDirect = 0
                      AND NOT EXISTS (
                          SELECT 1 FROM Members m2
                          WHERE m2.TeamId = m.TeamId AND m2.Role = 'Owner'
                      )
                )
                UPDATE Members
                SET Role = 'Owner'
                WHERE Id IN (SELECT Id FROM FirstAdmins WHERE rn = 1);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert: every Owner back to Admin so the old code (which only
            // knows Admin / Member) still works on the data.
            migrationBuilder.Sql("UPDATE Members SET Role = 'Admin' WHERE Role = 'Owner';");
        }
    }
}
