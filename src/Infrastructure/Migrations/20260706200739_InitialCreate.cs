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
                name: "CategoriasTicket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoriasTicket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Instituciones",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NombreCorto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InfoExtra = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instituciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Movimientos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Movimientos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prefijos",
                columns: table => new
                {
                    PrefijoInstitucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PrefijoMovimiento = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    UltimoValor = table.Column<int>(type: "int", nullable: false),
                    UltimoCodigo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prefijos", x => new { x.PrefijoInstitucion, x.PrefijoMovimiento });
                });

            migrationBuilder.CreateTable(
                name: "RolModuloAccesos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolModuloAccesos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                name: "TemasTicket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    HorasResolucion = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CategoriaId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemasTicket", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemasTicket_CategoriasTicket_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "CategoriasTicket",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NombreCorto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TramitesDefinicion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", nullable: false),
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
                name: "AsignacionesUsuario",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsignacionesUsuario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsignacionesUsuario_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AsignacionesUsuario_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioTemas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TemaId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioTemas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioTemas_TemasTicket_TemaId",
                        column: x => x.TemaId,
                        principalTable: "TemasTicket",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UsuarioTemas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Unidades",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NombreCorto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LogoUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Unidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Unidades_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contactos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Notas = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Origen = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contactos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contactos_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contactos_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Contactos_Unidades_UnidadId",
                        column: x => x.UnidadId,
                        principalTable: "Unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Expedientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", nullable: false),
                    AreaId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    OrigenExternoId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
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
                        name: "FK_Expedientes_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expedientes_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Expedientes_Unidades_UnidadId",
                        column: x => x.UnidadId,
                        principalTable: "Unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Reuniones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Titulo = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    OrigenExternoId = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Visibilidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Publica"),
                    CreadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RegistroToken = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWID()"),
                    RegistroAbierto = table.Column<bool>(type: "bit", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    Hora = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Duracion = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Modalidad = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Lugar = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    AreaId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    EsCapacitacionPlataforma = table.Column<bool>(type: "bit", nullable: false),
                    ObjetivoAgenda = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Desarrollo = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                        name: "FK_Reuniones_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reuniones_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reuniones_Unidades_UnidadId",
                        column: x => x.UnidadId,
                        principalTable: "Unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reuniones_Usuarios_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "ExpedienteEtapaAvances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    TramiteIndex = table.Column<int>(type: "int", nullable: false),
                    SubId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteEtapaAvances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteEtapaAvances_Expedientes_ExpedienteId",
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
                name: "Tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TemaId = table.Column<int>(type: "int", nullable: true),
                    Prioridad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstitucionId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    AreaId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    UnidadId = table.Column<string>(type: "nvarchar(120)", nullable: true),
                    Institucion = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    ExpedienteId = table.Column<int>(type: "int", nullable: true),
                    ExpedienteCodigo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ReportanteNombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReportanteCorreo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportanteTelefono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CreadoPorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AsignadoAId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AsignadoA = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NotaResolucion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tickets_Areas_AreaId",
                        column: x => x.AreaId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Instituciones_InstitucionId",
                        column: x => x.InstitucionId,
                        principalTable: "Instituciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_TemasTicket_TemaId",
                        column: x => x.TemaId,
                        principalTable: "TemasTicket",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tickets_Unidades_UnidadId",
                        column: x => x.UnidadId,
                        principalTable: "Unidades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Tickets_Usuarios_AsignadoAId",
                        column: x => x.AsignadoAId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tickets_Usuarios_CreadoPorId",
                        column: x => x.CreadoPorId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
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
                    Plazo = table.Column<DateOnly>(type: "date", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pendiente"),
                    FechaCumplimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    NotaSeguimiento = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SeguimientoActualizadoEl = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SeguimientoActualizadoPor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
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
                    Departamento = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AutoRegistro = table.Column<bool>(type: "bit", nullable: false),
                    RegistradoEl = table.Column<DateTime>(type: "datetime2", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "TicketAdjuntos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    ComentarioId = table.Column<int>(type: "int", nullable: true),
                    NombreArchivo = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    Tamano = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAdjuntos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketAdjuntos_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketComentarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Autor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketComentarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketComentarios_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TicketTramites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TicketId = table.Column<int>(type: "int", nullable: false),
                    TramiteDefinicionId = table.Column<int>(type: "int", nullable: true),
                    Tramite = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketTramites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TicketTramites_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Instituciones",
                columns: new[] { "Id", "Activo", "CreatedAt", "CreatedBy", "Descripcion", "InfoExtra", "LogoUrl", "Nombre", "NombreCorto", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { "1", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CONVIVIENDA", null, null, null },
                    { "10", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SEN", null, null, null },
                    { "11", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CONSUCOOP", null, null, null },
                    { "12", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CONATEL", null, null, null },
                    { "13", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHCINE", null, null, null },
                    { "14", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SAG", null, null, null },
                    { "15", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SECAPPH", null, null, null },
                    { "16", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SRECI", null, null, null },
                    { "17", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SERNA", null, null, null },
                    { "18", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SGJD", null, null, null },
                    { "19", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CANATURH / IHT", null, null, null },
                    { "2", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "COPECO", null, null, null },
                    { "20", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IP", null, null, null },
                    { "21", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SENASA", null, null, null },
                    { "22", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SESAL", null, null, null },
                    { "23", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "FOSOVI", null, null, null },
                    { "24", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHT", null, null, null },
                    { "3", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "SIT", null, null, null },
                    { "4", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHADFA", null, null, null },
                    { "5", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "BANHPROVI", null, null, null },
                    { "6", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "INPREUNAH", null, null, null },
                    { "7", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "CNBS", null, null, null },
                    { "8", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "INPREMA", null, null, null },
                    { "9", true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, null, null, null, "IHTT", null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_Estado",
                table: "AcuerdosReunion",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_Plazo",
                table: "AcuerdosReunion",
                column: "Plazo");

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_ReunionId",
                table: "AcuerdosReunion",
                column: "ReunionId");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_InstitucionId",
                table: "Areas",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesUsuario_InstitucionId",
                table: "AsignacionesUsuario",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_AsignacionesUsuario_UsuarioId_InstitucionId_AreaId_UnidadId",
                table: "AsignacionesUsuario",
                columns: new[] { "UsuarioId", "InstitucionId", "AreaId", "UnidadId" },
                unique: true,
                filter: "[AreaId] IS NOT NULL AND [UnidadId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Asistentes_ReunionId",
                table: "Asistentes",
                column: "ReunionId");

            migrationBuilder.CreateIndex(
                name: "IX_CategoriasTicket_Nombre",
                table: "CategoriasTicket",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Contactos_AreaId",
                table: "Contactos",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Contactos_Institucion",
                table: "Contactos",
                column: "Institucion");

            migrationBuilder.CreateIndex(
                name: "IX_Contactos_InstitucionId",
                table: "Contactos",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_Contactos_Nombre",
                table: "Contactos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Contactos_UnidadId",
                table: "Contactos",
                column: "UnidadId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosInternos_ExpedienteId",
                table: "DocumentosInternos",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentosSolicitados_ExpedienteId",
                table: "DocumentosSolicitados",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteEtapaAvances_ExpedienteId_TramiteIndex_SubId",
                table: "ExpedienteEtapaAvances",
                columns: new[] { "ExpedienteId", "TramiteIndex", "SubId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_AreaId",
                table: "Expedientes",
                column: "AreaId");

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
                name: "IX_Expedientes_OrigenExternoId",
                table: "Expedientes",
                column: "OrigenExternoId",
                unique: true,
                filter: "[OrigenExternoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Expedientes_UnidadId",
                table: "Expedientes",
                column: "UnidadId");

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
                name: "IX_Reuniones_AreaId",
                table: "Reuniones",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_CreadoPorId",
                table: "Reuniones",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_Fecha",
                table: "Reuniones",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_InstitucionId",
                table: "Reuniones",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_OrigenExternoId",
                table: "Reuniones",
                column: "OrigenExternoId",
                unique: true,
                filter: "[OrigenExternoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_RegistroToken",
                table: "Reuniones",
                column: "RegistroToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_UnidadId",
                table: "Reuniones",
                column: "UnidadId");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_Visibilidad_CreadoPorId",
                table: "Reuniones",
                columns: new[] { "Visibilidad", "CreadoPorId" });

            migrationBuilder.CreateIndex(
                name: "IX_RolModuloAccesos_Rol_Modulo",
                table: "RolModuloAccesos",
                columns: new[] { "Rol", "Modulo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemasTicket_CategoriaId",
                table: "TemasTicket",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_TemasTicket_Nombre",
                table: "TemasTicket",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TicketAdjuntos_TicketId",
                table: "TicketAdjuntos",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketComentarios_TicketId",
                table: "TicketComentarios",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AreaId",
                table: "Tickets",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_AsignadoAId",
                table: "Tickets",
                column: "AsignadoAId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreadoPorId",
                table: "Tickets",
                column: "CreadoPorId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_CreatedAt",
                table: "Tickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Estado",
                table: "Tickets",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_ExpedienteId",
                table: "Tickets",
                column: "ExpedienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_InstitucionId",
                table: "Tickets",
                column: "InstitucionId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_Numero",
                table: "Tickets",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TemaId",
                table: "Tickets",
                column: "TemaId");

            migrationBuilder.CreateIndex(
                name: "IX_Tickets_UnidadId",
                table: "Tickets",
                column: "UnidadId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketTramites_TicketId",
                table: "TicketTramites",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_TramiteRequisitos_ExpedienteId_TramiteIndex",
                table: "TramiteRequisitos",
                columns: new[] { "ExpedienteId", "TramiteIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TramitesDefinicion_InstitucionId_Orden",
                table: "TramitesDefinicion",
                columns: new[] { "InstitucionId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_Unidades_AreaId",
                table: "Unidades",
                column: "AreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Correo",
                table: "Usuarios",
                column: "Correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioTemas_TemaId",
                table: "UsuarioTemas",
                column: "TemaId");

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioTemas_UsuarioId_TemaId",
                table: "UsuarioTemas",
                columns: new[] { "UsuarioId", "TemaId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcuerdosReunion");

            migrationBuilder.DropTable(
                name: "AsignacionesUsuario");

            migrationBuilder.DropTable(
                name: "Asistentes");

            migrationBuilder.DropTable(
                name: "Contactos");

            migrationBuilder.DropTable(
                name: "DocumentosInternos");

            migrationBuilder.DropTable(
                name: "DocumentosSolicitados");

            migrationBuilder.DropTable(
                name: "ExpedienteEtapaAvances");

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
                name: "Movimientos");

            migrationBuilder.DropTable(
                name: "Prefijos");

            migrationBuilder.DropTable(
                name: "RolModuloAccesos");

            migrationBuilder.DropTable(
                name: "TicketAdjuntos");

            migrationBuilder.DropTable(
                name: "TicketComentarios");

            migrationBuilder.DropTable(
                name: "TicketTramites");

            migrationBuilder.DropTable(
                name: "TramiteRequisitos");

            migrationBuilder.DropTable(
                name: "TramitesDefinicion");

            migrationBuilder.DropTable(
                name: "UsuarioTemas");

            migrationBuilder.DropTable(
                name: "Reuniones");

            migrationBuilder.DropTable(
                name: "Tickets");

            migrationBuilder.DropTable(
                name: "Expedientes");

            migrationBuilder.DropTable(
                name: "TemasTicket");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Unidades");

            migrationBuilder.DropTable(
                name: "CategoriasTicket");

            migrationBuilder.DropTable(
                name: "Areas");

            migrationBuilder.DropTable(
                name: "Instituciones");
        }
    }
}
