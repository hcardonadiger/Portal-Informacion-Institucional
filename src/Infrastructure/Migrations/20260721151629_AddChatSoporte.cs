using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSoporte : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatSesiones",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    TecnicoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TecnicoNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    TemaId = table.Column<int>(type: "int", nullable: true),
                    TemaNombre = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    TicketId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Calificacion = table.Column<byte>(type: "tinyint", nullable: true),
                    Inicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Cierre = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSesiones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatMensajes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SesionId = table.Column<int>(type: "int", nullable: false),
                    Texto = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EsDelTecnico = table.Column<bool>(type: "bit", nullable: false),
                    EsSistema = table.Column<bool>(type: "bit", nullable: false),
                    AutorNombre = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Enviado = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Leido = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMensajes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMensajes_ChatSesiones_SesionId",
                        column: x => x.SesionId,
                        principalTable: "ChatSesiones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMensajes_SesionId_Leido",
                table: "ChatMensajes",
                columns: new[] { "SesionId", "Leido" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSesiones_Inicio",
                table: "ChatSesiones",
                column: "Inicio");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSesiones_TecnicoId_Estado",
                table: "ChatSesiones",
                columns: new[] { "TecnicoId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSesiones_UsuarioId",
                table: "ChatSesiones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatMensajes");

            migrationBuilder.DropTable(
                name: "ChatSesiones");
        }
    }
}
