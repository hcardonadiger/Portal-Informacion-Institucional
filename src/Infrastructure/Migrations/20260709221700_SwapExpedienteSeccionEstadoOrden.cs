using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SwapExpedienteSeccionEstadoOrden : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El wizard de Expedientes reordenó las secciones 4 y 5 (Modelo propuesto <-> Infraestructura
            // SOL). Los estados ya persistidos usan el numerado viejo, así que hay que intercambiarlos
            // para que sigan apuntando a la sección correcta. Se usa -1 como valor temporal para evitar
            // colisión durante el intercambio.
            migrationBuilder.Sql("UPDATE ExpedienteSecciones SET Seccion = -1 WHERE Seccion = 5;");
            migrationBuilder.Sql("UPDATE ExpedienteSecciones SET Seccion = 5  WHERE Seccion = 4;");
            migrationBuilder.Sql("UPDATE ExpedienteSecciones SET Seccion = 4  WHERE Seccion = -1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE ExpedienteSecciones SET Seccion = -1 WHERE Seccion = 4;");
            migrationBuilder.Sql("UPDATE ExpedienteSecciones SET Seccion = 4  WHERE Seccion = 5;");
            migrationBuilder.Sql("UPDATE ExpedienteSecciones SET Seccion = 5  WHERE Seccion = -1;");
        }
    }
}
