using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLevantamientosAndCronograma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpedienteEtapaCronogramas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    EtapaNum = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaRealFin = table.Column<DateOnly>(type: "date", nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Observacion = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteEtapaCronogramas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteEtapaCronogramas_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Levantamientos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Encargado = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Celular = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ObsEstado = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MigradaSOL = table.Column<bool>(type: "bit", nullable: false),
                    Limitante = table.Column<bool>(type: "bit", nullable: false),
                    LimitanteObs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Personal = table.Column<bool>(type: "bit", nullable: false),
                    PersonalObs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RequiereAcompanamiento = table.Column<bool>(type: "bit", nullable: false),
                    Habilidad = table.Column<bool>(type: "bit", nullable: false),
                    HabilidadObs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ObsGenerales = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Levantamientos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LevantamientoDocumentos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevantamientoId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Url = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    FechaDocumento = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevantamientoDocumentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LevantamientoDocumentos_Levantamientos_LevantamientoId",
                        column: x => x.LevantamientoId,
                        principalTable: "Levantamientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevantamientoEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevantamientoId = table.Column<int>(type: "int", nullable: false),
                    Funcion = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Contacto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevantamientoEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LevantamientoEquipo_Levantamientos_LevantamientoId",
                        column: x => x.LevantamientoId,
                        principalTable: "Levantamientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LevantamientoTramites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LevantamientoId = table.Column<int>(type: "int", nullable: false),
                    NombreTramite = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    ActaFirmada = table.Column<bool>(type: "bit", nullable: false),
                    RequiereMejoras = table.Column<bool>(type: "bit", nullable: false),
                    TieneInstructivo = table.Column<bool>(type: "bit", nullable: false),
                    Socializado = table.Column<bool>(type: "bit", nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LevantamientoTramites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LevantamientoTramites_Levantamientos_LevantamientoId",
                        column: x => x.LevantamientoId,
                        principalTable: "Levantamientos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteEtapaCronogramas_ExpedienteId_TramiteIndex_EtapaNum",
                table: "ExpedienteEtapaCronogramas",
                columns: new[] { "ExpedienteId", "TramiteIndex", "EtapaNum" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LevantamientoDocumentos_LevantamientoId",
                table: "LevantamientoDocumentos",
                column: "LevantamientoId");

            migrationBuilder.CreateIndex(
                name: "IX_LevantamientoEquipo_LevantamientoId",
                table: "LevantamientoEquipo",
                column: "LevantamientoId");

            migrationBuilder.CreateIndex(
                name: "IX_Levantamientos_CreatedAt",
                table: "Levantamientos",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Levantamientos_Estado",
                table: "Levantamientos",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Levantamientos_Institucion",
                table: "Levantamientos",
                column: "Institucion");

            migrationBuilder.CreateIndex(
                name: "IX_LevantamientoTramites_LevantamientoId_Orden",
                table: "LevantamientoTramites",
                columns: new[] { "LevantamientoId", "Orden" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpedienteEtapaCronogramas");

            migrationBuilder.DropTable(
                name: "LevantamientoDocumentos");

            migrationBuilder.DropTable(
                name: "LevantamientoEquipo");

            migrationBuilder.DropTable(
                name: "LevantamientoTramites");

            migrationBuilder.DropTable(
                name: "Levantamientos");
        }
    }
}
