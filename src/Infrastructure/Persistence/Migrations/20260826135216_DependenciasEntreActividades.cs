using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DependenciasEntreActividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProyectoDependenciasActividad",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SucesoraId = table.Column<int>(type: "int", nullable: false),
                    PredecesoraId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoDependenciasActividad", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoDependenciasActividad_ProyectoActividades_PredecesoraId",
                        column: x => x.PredecesoraId,
                        principalTable: "ProyectoActividades",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProyectoDependenciasActividad_ProyectoActividades_SucesoraId",
                        column: x => x.SucesoraId,
                        principalTable: "ProyectoActividades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoDependenciasActividad_PredecesoraId",
                table: "ProyectoDependenciasActividad",
                column: "PredecesoraId");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoDependenciasActividad_SucesoraId_PredecesoraId",
                table: "ProyectoDependenciasActividad",
                columns: new[] { "SucesoraId", "PredecesoraId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProyectoDependenciasActividad");
        }
    }
}
