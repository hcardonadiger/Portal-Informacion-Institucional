using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAcuerdoResponsableContacto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResponsableContactoId",
                table: "AcuerdosReunion",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcuerdosReunion_ResponsableContactoId",
                table: "AcuerdosReunion",
                column: "ResponsableContactoId");

            migrationBuilder.AddForeignKey(
                name: "FK_AcuerdosReunion_Contactos_ResponsableContactoId",
                table: "AcuerdosReunion",
                column: "ResponsableContactoId",
                principalTable: "Contactos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AcuerdosReunion_Contactos_ResponsableContactoId",
                table: "AcuerdosReunion");

            migrationBuilder.DropIndex(
                name: "IX_AcuerdosReunion_ResponsableContactoId",
                table: "AcuerdosReunion");

            migrationBuilder.DropColumn(
                name: "ResponsableContactoId",
                table: "AcuerdosReunion");
        }
    }
}
