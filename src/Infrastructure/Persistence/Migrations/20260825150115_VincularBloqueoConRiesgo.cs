using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VincularBloqueoConRiesgo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RiesgoId",
                table: "ProyectoAvances",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAvances_RiesgoId",
                table: "ProyectoAvances",
                column: "RiesgoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProyectoAvances_ProyectoRiesgos_RiesgoId",
                table: "ProyectoAvances",
                column: "RiesgoId",
                principalTable: "ProyectoRiesgos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProyectoAvances_ProyectoRiesgos_RiesgoId",
                table: "ProyectoAvances");

            migrationBuilder.DropIndex(
                name: "IX_ProyectoAvances_RiesgoId",
                table: "ProyectoAvances");

            migrationBuilder.DropColumn(
                name: "RiesgoId",
                table: "ProyectoAvances");
        }
    }
}
