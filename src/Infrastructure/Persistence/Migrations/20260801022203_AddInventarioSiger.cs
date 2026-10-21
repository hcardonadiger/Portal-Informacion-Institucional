using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventarioSiger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TramitesSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IdSiger = table.Column<int>(type: "int", nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Institucion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sigla = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Dependencia = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Objetivo = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DirigidoA = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EstadoSiger = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Publicado = table.Column<bool>(type: "bit", nullable: false),
                    DisponibleEnLinea = table.Column<bool>(type: "bit", nullable: false),
                    EnPlanDigitalizacion = table.Column<bool>(type: "bit", nullable: false),
                    VigenciaDocumento = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Temporalidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DiagramaUrl = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    EnlacePrincipal = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    ObservacionesDiger = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UltimaModificacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramitesSiger", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EnlacesSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnlacesSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnlacesSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EntregablesSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Entregable = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Formato = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Presentacion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntregablesSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntregablesSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LugaresAtencionSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Lugar = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Ciudad = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Direccion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Telefonos = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LugaresAtencionSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LugaresAtencionSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasosSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    NumeroPaso = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LugarDependencia = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SalidaResultado = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true),
                    TiempoRegistrado = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasosSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasosSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequisitosSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    Numero = table.Column<int>(type: "int", nullable: false),
                    Requisito = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DocumentoSoporte = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Formato = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequisitosSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequisitosSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TareasDigitalizacionSiger",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TramiteSigerId = table.Column<int>(type: "int", nullable: false),
                    NumeroTarea = table.Column<int>(type: "int", nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    FechaCumplimiento = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TareasDigitalizacionSiger", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TareasDigitalizacionSiger_TramitesSiger_TramiteSigerId",
                        column: x => x.TramiteSigerId,
                        principalTable: "TramitesSiger",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnlacesSiger_TramiteSigerId_Numero",
                table: "EnlacesSiger",
                columns: new[] { "TramiteSigerId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_EntregablesSiger_TramiteSigerId_Numero",
                table: "EntregablesSiger",
                columns: new[] { "TramiteSigerId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_LugaresAtencionSiger_TramiteSigerId_Numero",
                table: "LugaresAtencionSiger",
                columns: new[] { "TramiteSigerId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_PasosSiger_TramiteSigerId_NumeroPaso",
                table: "PasosSiger",
                columns: new[] { "TramiteSigerId", "NumeroPaso" });

            migrationBuilder.CreateIndex(
                name: "IX_RequisitosSiger_TramiteSigerId_Numero",
                table: "RequisitosSiger",
                columns: new[] { "TramiteSigerId", "Numero" });

            migrationBuilder.CreateIndex(
                name: "IX_TareasDigitalizacionSiger_TramiteSigerId_NumeroTarea",
                table: "TareasDigitalizacionSiger",
                columns: new[] { "TramiteSigerId", "NumeroTarea" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Codigo",
                table: "TramitesSiger",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_DisponibleEnLinea",
                table: "TramitesSiger",
                column: "DisponibleEnLinea");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_EnPlanDigitalizacion",
                table: "TramitesSiger",
                column: "EnPlanDigitalizacion");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_EstadoSiger",
                table: "TramitesSiger",
                column: "EstadoSiger");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_IdSiger",
                table: "TramitesSiger",
                column: "IdSiger",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Institucion",
                table: "TramitesSiger",
                column: "Institucion");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Publicado",
                table: "TramitesSiger",
                column: "Publicado");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_Sigla",
                table: "TramitesSiger",
                column: "Sigla");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnlacesSiger");

            migrationBuilder.DropTable(
                name: "EntregablesSiger");

            migrationBuilder.DropTable(
                name: "LugaresAtencionSiger");

            migrationBuilder.DropTable(
                name: "PasosSiger");

            migrationBuilder.DropTable(
                name: "RequisitosSiger");

            migrationBuilder.DropTable(
                name: "TareasDigitalizacionSiger");

            migrationBuilder.DropTable(
                name: "TramitesSiger");
        }
    }
}
