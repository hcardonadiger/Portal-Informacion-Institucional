using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LinkExpedienteTramiteSiger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TramiteSigerId",
                table: "ExpedienteTramites",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteTramites_TramiteSigerId",
                table: "ExpedienteTramites",
                column: "TramiteSigerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpedienteTramites_TramitesSiger_TramiteSigerId",
                table: "ExpedienteTramites",
                column: "TramiteSigerId",
                principalTable: "TramitesSiger",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpedienteTramites_TramitesSiger_TramiteSigerId",
                table: "ExpedienteTramites");

            migrationBuilder.DropIndex(
                name: "IX_ExpedienteTramites_TramiteSigerId",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "TramiteSigerId",
                table: "ExpedienteTramites");
        }
    }
}
