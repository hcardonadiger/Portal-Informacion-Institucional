using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddComentariosCompromisos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComentariosCompromisos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AcuerdoReunionId = table.Column<int>(type: "int", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    ArchivoNombre = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    ArchivoUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ArchivoTamano = table.Column<long>(type: "bigint", nullable: true),
                    CreadoPor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CreadoPorRol = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreadoEl = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComentariosCompromisos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComentariosCompromisos_AcuerdosReunion_AcuerdoReunionId",
                        column: x => x.AcuerdoReunionId,
                        principalTable: "AcuerdosReunion",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComentariosCompromisos_AcuerdoReunionId",
                table: "ComentariosCompromisos",
                column: "AcuerdoReunionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComentariosCompromisos");
        }
    }
}
