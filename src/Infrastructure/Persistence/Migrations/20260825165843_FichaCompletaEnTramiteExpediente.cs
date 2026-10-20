using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FichaCompletaEnTramiteExpediente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "ExpedienteTramites",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EsGratuito",
                table: "ExpedienteTramites",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "EstaEnSol",
                table: "ExpedienteTramites",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ModalidadDetalle",
                table: "ExpedienteTramites",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesDiger",
                table: "ExpedienteTramites",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SolTramo",
                table: "ExpedienteTramites",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Temporalidad",
                table: "ExpedienteTramites",
                type: "nvarchar(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VigenciaDocumento",
                table: "ExpedienteTramites",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExpedienteTramiteEntregables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Entregable = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Formato = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Presentacion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteTramiteEntregables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteTramiteEntregables_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpedienteTramiteLugares",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Lugar = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Ciudad = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Telefonos = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteTramiteLugares", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteTramiteLugares_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteTramites_CategoriaId",
                table: "ExpedienteTramites",
                column: "CategoriaId",
                filter: "[CategoriaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteTramiteEntregables_ExpedienteId_TramiteIndex",
                table: "ExpedienteTramiteEntregables",
                columns: new[] { "ExpedienteId", "TramiteIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteTramiteLugares_ExpedienteId_TramiteIndex",
                table: "ExpedienteTramiteLugares",
                columns: new[] { "ExpedienteId", "TramiteIndex" });

            migrationBuilder.AddForeignKey(
                name: "FK_ExpedienteTramites_CategoriasTramite_CategoriaId",
                table: "ExpedienteTramites",
                column: "CategoriaId",
                principalTable: "CategoriasTramite",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpedienteTramites_CategoriasTramite_CategoriaId",
                table: "ExpedienteTramites");

            migrationBuilder.DropTable(
                name: "ExpedienteTramiteEntregables");

            migrationBuilder.DropTable(
                name: "ExpedienteTramiteLugares");

            migrationBuilder.DropIndex(
                name: "IX_ExpedienteTramites_CategoriaId",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "EsGratuito",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "EstaEnSol",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "ModalidadDetalle",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "ObservacionesDiger",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "SolTramo",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "Temporalidad",
                table: "ExpedienteTramites");

            migrationBuilder.DropColumn(
                name: "VigenciaDocumento",
                table: "ExpedienteTramites");
        }
    }
}
