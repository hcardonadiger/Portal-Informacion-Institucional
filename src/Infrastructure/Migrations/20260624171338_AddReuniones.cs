using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReuniones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reuniones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    Hora = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Duracion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Modalidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Lugar = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    InstitucionId = table.Column<int>(type: "int", nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EsCapacitacionPlataforma = table.Column<bool>(type: "bit", nullable: false),
                    ObjetivoAgenda = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Desarrollo = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Tema = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ObjetivoCap = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Contenido = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EpNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EpCargo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EpCorreo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    EpTel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    FacNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FacCargo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FacCorreo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Convocados = table.Column<int>(type: "int", nullable: true),
                    NumAsistentes = table.Column<int>(type: "int", nullable: true),
                    PctAsistencia = table.Column<int>(type: "int", nullable: true),
                    Satisfaccion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Compromisos = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ValDiger = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValInst = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocsRecursos = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Foto1Url = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Foto1Desc = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Foto2Url = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    Foto2Desc = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reuniones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reuniones_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AcuerdosReunion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReunionId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Compromiso = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Plazo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcuerdosReunion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcuerdosReunion_Reuniones_ReunionId",
                        column: x => x.ReunionId,
                        principalTable: "Reuniones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Asistentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReunionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Asistentes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Asistentes_Reuniones_ReunionId",
                        column: x => x.ReunionId,
                        principalTable: "Reuniones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_ReunionId",
                table: "AcuerdosReunion",
                column: "ReunionId");

            migrationBuilder.CreateIndex(
                name: "IX_Asistentes_ReunionId",
                table: "Asistentes",
                column: "ReunionId");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_Fecha",
                table: "Reuniones",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_InstitucionId",
                table: "Reuniones",
                column: "InstitucionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcuerdosReunion");

            migrationBuilder.DropTable(
                name: "Asistentes");

            migrationBuilder.DropTable(
                name: "Reuniones");
        }
    }
}
