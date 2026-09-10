using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAlcanceProyecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstitucionId",
                table: "Proyectos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnidadId",
                table: "Proyectos",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_InstitucionId",
                table: "Proyectos",
                column: "InstitucionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Proyectos_InstitucionId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "InstitucionId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "UnidadId",
                table: "Proyectos");
        }
    }
}
