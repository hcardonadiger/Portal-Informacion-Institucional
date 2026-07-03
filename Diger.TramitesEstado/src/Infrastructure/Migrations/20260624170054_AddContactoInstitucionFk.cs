using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContactoInstitucionFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InstitucionId",
                table: "Contactos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Backfill: mapear la institución por nombre (snapshot) antes de crear la FK.
            migrationBuilder.Sql(
                "UPDATE c SET c.InstitucionId = i.Id " +
                "FROM Contactos c JOIN Instituciones i ON i.Nombre = c.Institucion;");
            // Eliminar contactos cuyo nombre de institución no exista en el catálogo.
            migrationBuilder.Sql("DELETE FROM Contactos WHERE InstitucionId = 0;");

            migrationBuilder.CreateIndex(
                name: "IX_Contactos_InstitucionId",
                table: "Contactos",
                column: "InstitucionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contactos_Instituciones_InstitucionId",
                table: "Contactos",
                column: "InstitucionId",
                principalTable: "Instituciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contactos_Instituciones_InstitucionId",
                table: "Contactos");

            migrationBuilder.DropIndex(
                name: "IX_Contactos_InstitucionId",
                table: "Contactos");

            migrationBuilder.DropColumn(
                name: "InstitucionId",
                table: "Contactos");
        }
    }
}
