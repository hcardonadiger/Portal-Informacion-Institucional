using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarPlantillasDeProyecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlantillasProyecto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Nivel = table.Column<int>(type: "int", nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillasProyecto", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaEntregables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantillaProyectoId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaEntregables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantillaEntregables_PlantillasProyecto_PlantillaProyectoId",
                        column: x => x.PlantillaProyectoId,
                        principalTable: "PlantillasProyecto",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlantillaActividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlantillaEntregableId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlantillaActividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlantillaActividades_PlantillaEntregables_PlantillaEntregableId",
                        column: x => x.PlantillaEntregableId,
                        principalTable: "PlantillaEntregables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaActividades_PlantillaEntregableId",
                table: "PlantillaActividades",
                column: "PlantillaEntregableId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillaEntregables_PlantillaProyectoId",
                table: "PlantillaEntregables",
                column: "PlantillaProyectoId");

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasProyecto_InstitucionId_Activa",
                table: "PlantillasProyecto",
                columns: new[] { "InstitucionId", "Activa" });

            migrationBuilder.CreateIndex(
                name: "IX_PlantillasProyecto_InstitucionId_Nombre",
                table: "PlantillasProyecto",
                columns: new[] { "InstitucionId", "Nombre" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlantillaActividades");

            migrationBuilder.DropTable(
                name: "PlantillaEntregables");

            migrationBuilder.DropTable(
                name: "PlantillasProyecto");
        }
    }
}
