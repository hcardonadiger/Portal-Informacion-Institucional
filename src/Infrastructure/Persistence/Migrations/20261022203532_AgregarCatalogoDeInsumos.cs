using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Catálogo de insumos (entrega 4, 2026-10-22): sus categorías, los insumos y la asignación de
    /// insumos a las actividades de un proyecto. Es aditiva: no toca ninguna tabla existente.
    ///
    /// <para>Siembra las seis categorías iniciales <b>solo si la tabla está vacía</b>, no nombre
    /// por nombre. Son editables: si alguien renombra «Otros» a «Varios», una siembra por nombre
    /// volvería a crear «Otros» en la próxima corrida del script de producción. Mismo criterio en
    /// <c>database/2026-10-22_poner_al_dia.sql</c>.</para>
    /// </summary>
    public partial class AgregarCatalogoDeInsumos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoriasInsumo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasInsumo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Insumos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoriaId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insumos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Insumos_CategoriasInsumo_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasInsumo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProyectoActividadInsumos",
                columns: table => new
                {
                    ActividadId = table.Column<int>(type: "int", nullable: false),
                    InsumoId = table.Column<int>(type: "int", nullable: false),
                    AgregadoEl = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgregadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoActividadInsumos", x => new { x.ActividadId, x.InsumoId });
                    table.ForeignKey(
                        name: "FK_ProyectoActividadInsumos_Insumos_InsumoId",
                        column: x => x.InsumoId,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProyectoActividadInsumos_ProyectoActividades_ActividadId",
                        column: x => x.ActividadId,
                        principalTable: "ProyectoActividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasInsumo_Nombre",
                table: "CategoriasInsumo",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasInsumo_Orden",
                table: "CategoriasInsumo",
                column: "Orden");

            migrationBuilder.CreateIndex(
                name: "IX_Insumos_CategoriaId_Nombre",
                table: "Insumos",
                columns: new[] { "CategoriaId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoActividadInsumos_InsumoId",
                table: "ProyectoActividadInsumos",
                column: "InsumoId");

            // Dentro de EXEC por lo mismo que en CatalogoDePrioridadesDeProyecto: el script que se
            // genera para producción es un solo lote, y SQL Server lo compila entero antes de que
            // exista la tabla.
            migrationBuilder.Sql(@"
EXEC(N'
IF NOT EXISTS (SELECT 1 FROM CategoriasInsumo)
    INSERT INTO CategoriasInsumo (Nombre, Orden, Activo, CreatedAt, CreatedBy)
    VALUES (N''Materiales'',          1, 1, SYSUTCDATETIME(), N''migracion''),
           (N''Equipo y maquinaria'', 2, 1, SYSUTCDATETIME(), N''migracion''),
           (N''Herramientas'',        3, 1, SYSUTCDATETIME(), N''migracion''),
           (N''Equipo informático'',  4, 1, SYSUTCDATETIME(), N''migracion''),
           (N''Servicios'',           5, 1, SYSUTCDATETIME(), N''migracion''),
           (N''Otros'',               6, 1, SYSUTCDATETIME(), N''migracion'');
');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProyectoActividadInsumos");

            migrationBuilder.DropTable(
                name: "Insumos");

            migrationBuilder.DropTable(
                name: "CategoriasInsumo");
        }
    }
}
