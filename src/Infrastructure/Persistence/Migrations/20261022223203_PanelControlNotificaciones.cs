using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PanelControlNotificaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Cantidad",
                table: "Notificaciones",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaActualizacion",
                table: "Notificaciones",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateTable(
                name: "AjustesNotificaciones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    IntervaloMinutos = table.Column<int>(type: "int", nullable: false),
                    SmtpConfigurado = table.Column<bool>(type: "bit", nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPuerto = table.Column<int>(type: "int", nullable: false),
                    SmtpSsl = table.Column<bool>(type: "bit", nullable: false),
                    SmtpUsuario = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpRemitente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpNombreRemitente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPasswordCifrada = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UltimaRevisionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AjustesNotificaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CorreosPendientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DestinatarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Modulo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Clave = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreadoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Intentos = table.Column<int>(type: "int", nullable: false),
                    UltimoError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EnviadoUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CorreosPendientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModulosNotificacion",
                columns: table => new
                {
                    Modulo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CampanaActiva = table.Column<bool>(type: "bit", nullable: false),
                    CorreoActivo = table.Column<bool>(type: "bit", nullable: false),
                    Agrupar = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModulosNotificacion", x => x.Modulo);
                });

            migrationBuilder.CreateTable(
                name: "NotificacionElementos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificacionId = table.Column<int>(type: "int", nullable: false),
                    Clave = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificacionElementos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificacionElementos_Notificaciones_NotificacionId",
                        column: x => x.NotificacionId,
                        principalTable: "Notificaciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PanelControlBitacora",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Seccion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Detalle = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PanelControlBitacora", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CorreosPendientes_Estado_DestinatarioId",
                table: "CorreosPendientes",
                columns: new[] { "Estado", "DestinatarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionElementos_Clave_FechaUtc",
                table: "NotificacionElementos",
                columns: new[] { "Clave", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificacionElementos_NotificacionId",
                table: "NotificacionElementos",
                column: "NotificacionId");

            migrationBuilder.CreateIndex(
                name: "IX_PanelControlBitacora_Fecha",
                table: "PanelControlBitacora",
                column: "Fecha");

            // Los avisos que ya existen pasan a tener su elemento y su fecha de actualización: sin
            // eso, el primer día no sabrían que ya se avisó lo que avisaron y lo repetirían.
            migrationBuilder.Sql("UPDATE Notificaciones SET FechaActualizacion = FechaCreacion;");
            migrationBuilder.Sql(@"
                INSERT INTO NotificacionElementos (NotificacionId, Clave, Titulo, Url, FechaUtc)
                SELECT Id, LEFT(COALESCE(Url, Titulo), 500), Titulo, Url, FechaCreacion
                FROM Notificaciones;");

            // Valores iniciales: correo apagado en todos los módulos (decisión del 2026-10-22).
            migrationBuilder.Sql(@"
                INSERT INTO AjustesNotificaciones (Id, IntervaloMinutos, SmtpConfigurado, SmtpPuerto, SmtpSsl, CreatedAt)
                VALUES (1, 240, 0, 587, 1, SYSUTCDATETIME());");
            migrationBuilder.Sql(@"
                INSERT INTO ModulosNotificacion (Modulo, CampanaActiva, CorreoActivo, Agrupar) VALUES
                (N'Tickets', 1, 0, 1), (N'Reuniones', 1, 0, 1), (N'Expedientes', 1, 0, 1),
                (N'Proyectos', 1, 0, 1), (N'Soporte', 1, 0, 1);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AjustesNotificaciones");

            migrationBuilder.DropTable(
                name: "CorreosPendientes");

            migrationBuilder.DropTable(
                name: "ModulosNotificacion");

            migrationBuilder.DropTable(
                name: "NotificacionElementos");

            migrationBuilder.DropTable(
                name: "PanelControlBitacora");

            migrationBuilder.DropColumn(
                name: "Cantidad",
                table: "Notificaciones");

            migrationBuilder.DropColumn(
                name: "FechaActualizacion",
                table: "Notificaciones");
        }
    }
}
