using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeguimientoPorTramite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpedienteEtapaAvances_ExpedienteId_SubId",
                table: "ExpedienteEtapaAvances");

            migrationBuilder.AddColumn<int>(
                name: "TramiteIndex",
                table: "ExpedienteEtapaAvances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteEtapaAvances_ExpedienteId_TramiteIndex_SubId",
                table: "ExpedienteEtapaAvances",
                columns: new[] { "ExpedienteId", "TramiteIndex", "SubId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExpedienteEtapaAvances_ExpedienteId_TramiteIndex_SubId",
                table: "ExpedienteEtapaAvances");

            migrationBuilder.DropColumn(
                name: "TramiteIndex",
                table: "ExpedienteEtapaAvances");

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteEtapaAvances_ExpedienteId_SubId",
                table: "ExpedienteEtapaAvances",
                columns: new[] { "ExpedienteId", "SubId" },
                unique: true);
        }
    }
}
