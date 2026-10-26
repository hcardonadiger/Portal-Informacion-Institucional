using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExpedienteEquipoInstitucional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpedienteEquipo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExpedienteId = table.Column<int>(type: "int", nullable: false),
                    Prioridad = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Cargo = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Correo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpedienteEquipo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpedienteEquipo_Expedientes_ExpedienteId",
                        column: x => x.ExpedienteId,
                        principalTable: "Expedientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpedienteEquipo_ExpedienteId",
                table: "ExpedienteEquipo",
                column: "ExpedienteId");

            // El contacto que ya tenía cada expediente abre su equipo como prioridad 1. Es el
            // mismo paso que trae database/2026-10-26_poner_al_dia.sql.
            migrationBuilder.Sql("""
                INSERT INTO [ExpedienteEquipo] ([ExpedienteId], [Prioridad], [Nombre], [Cargo], [Correo], [Telefono])
                SELECT e.[Id], 1, LTRIM(RTRIM(e.[ContactoNombre])),
                       NULLIF(LTRIM(RTRIM(e.[ContactoCargo])), N''),
                       NULLIF(LTRIM(RTRIM(e.[ContactoCorreo])), N''),
                       NULLIF(LTRIM(RTRIM(e.[ContactoTel])), N'')
                FROM [Expedientes] e
                WHERE NULLIF(LTRIM(RTRIM(e.[ContactoNombre])), N'') IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM [ExpedienteEquipo] q WHERE q.[ExpedienteId] = e.[Id]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExpedienteEquipo");
        }
    }
}
