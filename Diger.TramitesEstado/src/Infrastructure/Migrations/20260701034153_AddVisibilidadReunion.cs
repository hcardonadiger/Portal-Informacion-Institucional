using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisibilidadReunion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CreadoPorId",
                table: "Reuniones",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Visibilidad",
                table: "Reuniones",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Publica");

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_Visibilidad_CreadoPorId",
                table: "Reuniones",
                columns: new[] { "Visibilidad", "CreadoPorId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reuniones_Visibilidad_CreadoPorId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "CreadoPorId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "Visibilidad",
                table: "Reuniones");
        }
    }
}
