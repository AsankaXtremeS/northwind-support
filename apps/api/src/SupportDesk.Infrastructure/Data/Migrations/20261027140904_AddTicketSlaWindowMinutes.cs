using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaWindowMinutes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SlaWindowMinutes",
                table: "Tickets",
                type: "int",
                nullable: true);

            // No ticket has been escalated yet, so every existing window ran from creation to due.
            // A due date that is not after creation has no meaningful window and stays NULL.
            // EXEC defers compiling the UPDATE, so a generated script run as one batch does not
            // reject the column added just above.
            migrationBuilder.Sql(
                """
                EXEC(N'UPDATE Tickets
                SET SlaWindowMinutes = DATEDIFF(minute, CreatedAtUtc, DueAtUtc)
                WHERE DueAtUtc IS NOT NULL
                  AND DATEDIFF(minute, CreatedAtUtc, DueAtUtc) > 0;');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Tickets_SlaWindowMinutes",
                table: "Tickets",
                sql: "[SlaWindowMinutes] > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Tickets_SlaWindowMinutes",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaWindowMinutes",
                table: "Tickets");
        }
    }
}
