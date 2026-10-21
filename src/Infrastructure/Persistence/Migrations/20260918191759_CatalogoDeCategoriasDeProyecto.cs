using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega el catálogo administrable CategoriasProyecto y la columna opcional que lo apunta
    /// desde Proyectos.
    ///
    /// <para>A diferencia de las dos migraciones de prioridades, esta va tal como la generó el
    /// andamiaje y no hizo falta reordenarla: no hay columna vieja que borrar ni contenido que
    /// trasladar, porque la categoría nace en nulo. Los proyectos que ya existen quedan «sin
    /// clasificar», que es un valor legítimo y no un hueco: es el mismo criterio con el que se
    /// agregó AccionProyecto.</para>
    ///
    /// <para>Tampoco siembra categorías. Inventarlas acá sería exactamente lo que el nulo evita
    /// —una clasificación que nadie decidió, indistinguible de una declarada—; las crea quien
    /// administre, en Catálogos › Categorías de proyectos.</para>
    /// </summary>
    public partial class CatalogoDeCategoriasDeProyecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "Proyectos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CategoriasProyecto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Color = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasProyecto", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Proyectos_CategoriaId",
                table: "Proyectos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProyecto_Nombre",
                table: "CategoriasProyecto",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasProyecto_Orden",
                table: "CategoriasProyecto",
                column: "Orden");

            migrationBuilder.AddForeignKey(
                name: "FK_Proyectos_CategoriasProyecto_CategoriaId",
                table: "Proyectos",
                column: "CategoriaId",
                principalTable: "CategoriasProyecto",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Proyectos_CategoriasProyecto_CategoriaId",
                table: "Proyectos");

            migrationBuilder.DropTable(
                name: "CategoriasProyecto");

            migrationBuilder.DropIndex(
                name: "IX_Proyectos_CategoriaId",
                table: "Proyectos");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "Proyectos");
        }
    }
}
