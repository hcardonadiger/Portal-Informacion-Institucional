using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreregistroAsistente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsPreregistro",
                table: "Asistentes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "Confirmado",
                table: "Asistentes",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Asistentes_EsPreregistro",
                table: "Asistentes",
                column: "EsPreregistro");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Asistentes_EsPreregistro",
                table: "Asistentes");

            migrationBuilder.DropColumn(
                name: "EsPreregistro",
                table: "Asistentes");

            migrationBuilder.DropColumn(
                name: "Confirmado",
                table: "Asistentes");
        }
    }
}
