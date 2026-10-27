using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlantillaCategoriaYAccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Accion",
                table: "PlantillasProyecto",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "PlantillasProyecto",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasProyecto_CategoriaId",
                table: "PlantillasProyecto",
                column: "CategoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlantillasProyecto_CategoriasProyecto_CategoriaId",
                table: "PlantillasProyecto",
                column: "CategoriaId",
                principalTable: "CategoriasProyecto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PlantillasProyecto_CategoriasProyecto_CategoriaId",
                table: "PlantillasProyecto");

            migrationBuilder.DropIndex(
                name: "IX_PlantillasProyecto_CategoriaId",
                table: "PlantillasProyecto");

            migrationBuilder.DropColumn(
                name: "Accion",
                table: "PlantillasProyecto");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "PlantillasProyecto");
        }
    }
}
