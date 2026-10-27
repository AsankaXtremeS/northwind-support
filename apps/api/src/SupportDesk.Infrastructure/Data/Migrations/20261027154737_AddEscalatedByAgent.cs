using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEscalatedByAgent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "EscalatedBy",
                table: "TicketEscalations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "EscalatedByAgentId",
                table: "TicketEscalations",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketEscalations_EscalatedByAgentId",
                table: "TicketEscalations",
                column: "EscalatedByAgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_TicketEscalations_Agents_EscalatedByAgentId",
                table: "TicketEscalations",
                column: "EscalatedByAgentId",
                principalTable: "Agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TicketEscalations_Agents_EscalatedByAgentId",
                table: "TicketEscalations");

            migrationBuilder.DropIndex(
                name: "IX_TicketEscalations_EscalatedByAgentId",
                table: "TicketEscalations");

            migrationBuilder.DropColumn(
                name: "EscalatedByAgentId",
                table: "TicketEscalations");

            migrationBuilder.AlterColumn<string>(
                name: "EscalatedBy",
                table: "TicketEscalations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);
        }
    }
}
