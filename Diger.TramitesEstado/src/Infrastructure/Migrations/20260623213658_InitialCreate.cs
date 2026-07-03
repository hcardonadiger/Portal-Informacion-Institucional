using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instituciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instituciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Rol = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expedientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InstitucionId = table.Column<int>(type: "int", nullable: false),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    FechaApertura = table.Column<DateOnly>(type: "date", nullable: true),
                    Analista = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    DirSede = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    NumTramitesProd = table.Column<int>(type: "int", nullable: false),
                    ContactoNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContactoCargo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ContactoCorreo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ContactoTel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ObsLegal = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NumFuncionarios = table.Column<int>(type: "int", nullable: true),
                    VolumenAnual = table.Column<int>(type: "int", nullable: true),
                    TiempoObservado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TiempoNorma = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DescProceso = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DocsAdicionales = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObsFlujo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FuncionariosDig = table.Column<int>(type: "int", nullable: true),
                    TiempoDig = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ObsModelo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InfraPersonal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    InfraPersonalTI = table.Column<int>(type: "int", nullable: true),
                    InfraRespSol = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InfraAcomp = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    InfraDcModalidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    InfraDcVirt = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    InfraDcVirtOtro = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    InfraDcDisp = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    InfraDcObs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InfraPlan = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    EstadoExpediente = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EstadoLevantamiento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ObsExpediente = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ObsLevantamiento = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValidadoDiger = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ValidadoInst = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FechaValidacion = table.Column<DateOnly>(type: "date", nullable: true),
                    NumActa = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expedientes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expedientes_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TramitesDefinicion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitucionId = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramitesDefinicion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TramitesDefinicion_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosInternos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Documento = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Obs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosInternos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosInternos_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DocumentosSolicitados",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Recibido = table.Column<bool>(type: "bit", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentosSolicitados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentosSolicitados_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpedienteSecciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Seccion = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nota = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteSecciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteSecciones_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExpedienteTramites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    NombreTramite = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    NombreCorto = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AreaResponsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Modalidad = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PlazoLegal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Tercero = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TiempoReal = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MetodoPago = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PagoBanco = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    PagoCuenta = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    TgrInst = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TgrRubro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TgrMonto = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    DocEntregado = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Objetivo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Alcance = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    AlcanceObs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Descripcion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Dirigido = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Horario = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EmailTramite = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SitioWeb = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteTramites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteTramites_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlujoNodos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    Fase = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Area = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tiempo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DocEmitido = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Obs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    RetornoA = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlujoNodos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlujoNodos_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FundamentosLegales",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Instrumento = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Articulos = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Obs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundamentosLegales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FundamentosLegales_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InfraChecklist",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Grupo = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Requisito = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Obs = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfraChecklist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfraChecklist_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InfraCondiciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Condicion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfraCondiciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfraCondiciones_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InfraPerfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Perfil = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InfraPerfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InfraPerfiles_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TramiteRequisitos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Requisito = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Obs = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Accion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Justificacion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TramiteRequisitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TramiteRequisitos_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Instituciones",
                columns: new[] { "Id", "Activo", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "CONVIVIENDA" },
                    { 2, true, "COPECO" },
                    { 3, true, "SIT" },
                    { 4, true, "IHADFA" },
                    { 5, true, "BANHPROVI" },
                    { 6, true, "INPREUNAH" },
                    { 7, true, "CNBS" },
                    { 8, true, "INPREMA" },
                    { 9, true, "IHTT" },
                    { 10, true, "SEN" },
                    { 11, true, "CONSUCOOP" },
                    { 12, true, "CONATEL" },
                    { 13, true, "IHCINE" },
                    { 14, true, "SAG" },
                    { 15, true, "SECAPPH" },
                    { 16, true, "SRECI" },
                    { 17, true, "SERNA" },
                    { 18, true, "SGJD" },
                    { 19, true, "CANATURH / IHT" },
                    { 20, true, "IP" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosInternos_ExpedienteId",
                table: "DocumentosInternos",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosSolicitados_ExpedienteId",
                table: "DocumentosSolicitados",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_Codigo",
                table: "Expedientes",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_CreatedAt",
                table: "Expedientes",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_EstadoExpediente",
                table: "Expedientes",
                column: "EstadoExpediente");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_InstitucionId",
                table: "Expedientes",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteSecciones_ExpedienteId",
                table: "ExpedienteSecciones",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteTramites_ExpedienteId_TramiteIndex",
                table: "ExpedienteTramites",
                columns: new[] { "ExpedienteId", "TramiteIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_FlujoNodos_ExpedienteId_TramiteIndex_Fase",
                table: "FlujoNodos",
                columns: new[] { "ExpedienteId", "TramiteIndex", "Fase" });

            migrationBuilder.CreateIndex(
                name: "IX_FundamentosLegales_ExpedienteId",
                table: "FundamentosLegales",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_InfraChecklist_ExpedienteId",
                table: "InfraChecklist",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_InfraCondiciones_ExpedienteId",
                table: "InfraCondiciones",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_InfraPerfiles_ExpedienteId",
                table: "InfraPerfiles",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Instituciones_Nombre",
                table: "Instituciones",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramiteRequisitos_ExpedienteId_TramiteIndex",
                table: "TramiteRequisitos",
                columns: new[] { "ExpedienteId", "TramiteIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesDefinicion_InstitucionId_Orden",
                table: "TramitesDefinicion",
                columns: new[] { "InstitucionId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentosInternos");

            migrationBuilder.DropTable(
                name: "DocumentosSolicitados");

            migrationBuilder.DropTable(
                name: "ExpedienteSecciones");

            migrationBuilder.DropTable(
                name: "ExpedienteTramites");

            migrationBuilder.DropTable(
                name: "FlujoNodos");

            migrationBuilder.DropTable(
                name: "FundamentosLegales");

            migrationBuilder.DropTable(
                name: "InfraChecklist");

            migrationBuilder.DropTable(
                name: "InfraCondiciones");

            migrationBuilder.DropTable(
                name: "InfraPerfiles");

            migrationBuilder.DropTable(
                name: "TramiteRequisitos");

            migrationBuilder.DropTable(
                name: "TramitesDefinicion");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Expedientes");

            migrationBuilder.DropTable(
                name: "Instituciones");
        }
    }
}
