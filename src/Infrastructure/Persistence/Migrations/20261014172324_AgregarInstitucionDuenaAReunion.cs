using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarInstitucionDuenaAReunion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "InstitucionDuenaId",
                table: "Reuniones",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId",
                table: "Reuniones",
                columns: new[] { "InstitucionDuenaId", "AreaId", "UnidadId" });

            // El relleno va EN la migración, no después: la columna nueva es el ancla del respaldo
            // del filtro de Reunion. Una base con la columna agregada y vacía deja a todas las
            // reuniones sin área ni unidad —80 de 81 en la base de hoy— invisibles para todo el
            // mundo salvo su creador y un administrador global. Agregar y rellenar son un solo acto.
            //
            // Regla: la institución de quien la creó; y si no hay creador —las 78 importadas del
            // portal legado no lo tienen— la casa, que se deduce de dónde está asignada la mayoría
            // de los usuarios en vez de escribir una sigla aquí dentro. Solo toca filas en null, así
            // que correrla dos veces no cambia nada.
            //
            // Solo en SQL Server. El relleno usa DECLARE, TOP 1 y UPDATE...FROM, que son T-SQL;
            // sin esta guarda la migración revienta sobre el SQLite de Web.Tests y, al morir ahí,
            // no se crea NINGUNA tabla: 46 pruebas caían con 404 y «no such table: Roles», que no
            // se parece en nada a la causa. Las pruebas no necesitan el relleno —arrancan vacías—
            // y las bases desplegadas son todas SQL Server.
            if (migrationBuilder.ActiveProvider != "Microsoft.EntityFrameworkCore.SqlServer") return;

            migrationBuilder.Sql(@"
DECLARE @casa nvarchar(120) = (
    SELECT TOP 1 InstitucionId FROM AsignacionesUsuario
    GROUP BY InstitucionId ORDER BY COUNT(DISTINCT UsuarioId) DESC);

UPDATE r SET r.InstitucionDuenaId = COALESCE(
        (SELECT TOP 1 a.InstitucionId FROM AsignacionesUsuario a WHERE a.UsuarioId = r.CreadoPorId),
        @casa)
FROM Reuniones r
WHERE r.InstitucionDuenaId IS NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId",
                table: "Reuniones");

            migrationBuilder.DropColumn(
                name: "InstitucionDuenaId",
                table: "Reuniones");
        }
    }
}
