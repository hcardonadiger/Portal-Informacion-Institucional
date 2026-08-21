using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SigerIdOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_IdSiger",
                table: "TramitesSiger");

            migrationBuilder.AlterColumn<int>(
                name: "IdSiger",
                table: "TramitesSiger",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_IdSiger",
                table: "TramitesSiger",
                column: "IdSiger",
                unique: true,
                filter: "[IdSiger] IS NOT NULL");
        }

        /// <inheritdoc />
        /// <remarks>
        /// <b>Revertir no es inocuo.</b> Al volver la columna a no-nula, toda ficha promovida
        /// desde un expediente pasa a IdSiger = 0, y el índice único sin filtro solo admite una:
        /// con dos o más fichas promovidas, este Down falla al recrear el índice. Antes de
        /// revertir hay que decidir qué se hace con esas fichas — borrarlas o asignarles un
        /// IdSiger real — porque la reversión sola no puede saberlo.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TramitesSiger_IdSiger",
                table: "TramitesSiger");

            migrationBuilder.AlterColumn<int>(
                name: "IdSiger",
                table: "TramitesSiger",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TramitesSiger_IdSiger",
                table: "TramitesSiger",
                column: "IdSiger",
                unique: true);
        }
    }
}
