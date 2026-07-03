using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TemasAdministrables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Catálogo de temas + tabla de especialidad por tema.
            migrationBuilder.CreateTable(
                name: "TemasTicket",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    HorasResolucion = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Activo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemasTicket", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UsuarioTemas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
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

            // 2) Semilla de los temas iniciales (equivalentes al enum anterior) con SLA por defecto.
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [TemasTicket] ON;
INSERT INTO [TemasTicket] ([Id],[Nombre],[HorasResolucion],[Activo],[CreatedAt]) VALUES
 (1, N'Acceso', 24, 1, SYSUTCDATETIME()),
 (2, N'Error en plataforma', 8, 1, SYSUTCDATETIME()),
 (3, N'Configuración', 48, 1, SYSUTCDATETIME()),
 (4, N'Datos', 72, 1, SYSUTCDATETIME()),
 (5, N'Capacitación', 72, 1, SYSUTCDATETIME()),
 (6, N'Otro', 72, 1, SYSUTCDATETIME());
SET IDENTITY_INSERT [TemasTicket] OFF;");

            // 3) Nueva columna TemaId en Tickets + mapeo desde la categoría (enum en texto).
            migrationBuilder.AddColumn<int>(
                name: "TemaId",
                table: "Tickets",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE [Tickets] SET [TemaId] = CASE [Categoria]
  WHEN 'Acceso'          THEN 1
  WHEN 'ErrorPlataforma' THEN 2
  WHEN 'Configuracion'   THEN 3
  WHEN 'Datos'           THEN 4
  WHEN 'Capacitacion'    THEN 5
  WHEN 'Otro'            THEN 6
  ELSE NULL END;");

            // 4) Migrar la especialidad de usuarios (UsuarioCategorias → UsuarioTemas).
            migrationBuilder.Sql(@"
INSERT INTO [UsuarioTemas] ([UsuarioId],[TemaId])
SELECT [UsuarioId], CASE [Categoria]
  WHEN 'Acceso'          THEN 1
  WHEN 'ErrorPlataforma' THEN 2
  WHEN 'Configuracion'   THEN 3
  WHEN 'Datos'           THEN 4
  WHEN 'Capacitacion'    THEN 5
  WHEN 'Otro'            THEN 6
  END
FROM [UsuarioCategorias]
WHERE [Categoria] IN ('Acceso','ErrorPlataforma','Configuracion','Datos','Capacitacion','Otro');");

            // 5) Retirar las estructuras antiguas.
            migrationBuilder.DropTable(name: "UsuarioCategorias");
            migrationBuilder.DropColumn(name: "Categoria", table: "Tickets");

            // 6) Índices y clave foránea del tema.
            migrationBuilder.CreateIndex(
                name: "IX_Tickets_TemaId",
                table: "Tickets",
                column: "TemaId");

            migrationBuilder.CreateIndex(
                name: "IX_TemasTicket_Nombre",
                table: "TemasTicket",
                column: "Nombre",
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

            migrationBuilder.AddForeignKey(
                name: "FK_Tickets_TemasTicket_TemaId",
                table: "Tickets",
                column: "TemaId",
                principalTable: "TemasTicket",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tickets_TemasTicket_TemaId",
                table: "Tickets");

            migrationBuilder.DropTable(
                name: "UsuarioTemas");

            migrationBuilder.DropTable(
                name: "TemasTicket");

            migrationBuilder.DropIndex(
                name: "IX_Tickets_TemaId",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "TemaId",
                table: "Tickets");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Tickets",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "UsuarioCategorias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Categoria = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioCategorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UsuarioCategorias_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UsuarioCategorias_UsuarioId_Categoria",
                table: "UsuarioCategorias",
                columns: new[] { "UsuarioId", "Categoria" },
                unique: true);
        }
    }
}
