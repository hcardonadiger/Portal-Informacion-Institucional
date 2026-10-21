using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Diger.TramitesEstado.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Pasa el seguimiento de proyectos de una lista plana de hitos a la EDT del PMI:
    /// proyecto → entregables → actividades, con el avance reportado en la actividad.
    ///
    /// <para><b>Los hitos NO se borran: se renombran.</b> El andamiaje que genera EF proponía
    /// <c>DropTable ProyectoHitos</c> + <c>CreateTable ProyectoEntregables</c>, que se llevaría por
    /// delante los entregables cargados desde las actas y dejaría a <c>ProyectoAvances</c> con
    /// <c>EntregableId</c> apuntando a filas inexistentes — la FK nueva ni siquiera se podría crear.
    /// Acá la tabla se renombra con sus filas, sus Ids y sus índices, y las actividades entran como
    /// tabla nueva colgando de ella.</para>
    ///
    /// <para>Los nombres de PK y FK se renombran a mano con <c>sp_rename</c>: renombrar una tabla no
    /// arrastra los de sus restricciones, y dejarlas como <c>PK_ProyectoHitos</c> haría fallar a
    /// cualquier migración futura que las nombre por el nombre que el modelo cree que tienen.</para>
    /// </summary>
    public partial class EstructuraEntregablesActividades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La FK se suelta antes de tocar la tabla y se vuelve a crear al final, ya con los
            // nombres nuevos de los dos lados.
            migrationBuilder.DropForeignKey(
                name: "FK_ProyectoAvances_ProyectoHitos_HitoId",
                table: "ProyectoAvances");

            // ── Hitos → Entregables (la misma tabla, con sus filas) ──────────────
            migrationBuilder.RenameTable(
                name: "ProyectoHitos",
                newName: "ProyectoEntregables");

            migrationBuilder.RenameIndex(
                name: "IX_ProyectoHitos_ProyectoId_Orden",
                table: "ProyectoEntregables",
                newName: "IX_ProyectoEntregables_ProyectoId_Orden");

            migrationBuilder.Sql(
                "EXEC sp_rename N'PK_ProyectoHitos', N'PK_ProyectoEntregables', N'OBJECT';");
            migrationBuilder.Sql(
                "EXEC sp_rename N'FK_ProyectoHitos_Proyectos_ProyectoId', " +
                "N'FK_ProyectoEntregables_Proyectos_ProyectoId', N'OBJECT';");

            // ── La imputación de la bitácora conserva sus datos ──────────────────
            migrationBuilder.RenameColumn(
                name: "HitoId",
                table: "ProyectoAvances",
                newName: "EntregableId");

            migrationBuilder.RenameIndex(
                name: "IX_ProyectoAvances_HitoId",
                table: "ProyectoAvances",
                newName: "IX_ProyectoAvances_EntregableId");

            // Pasa a nullable: el porcentaje ahora es el de una actividad, y una entrada que no se
            // imputa a ninguna no reporta número. Las filas viejas conservan el suyo, que
            // significaba otra cosa —el avance del proyecto declarado a mano— y por eso no se
            // reinterpretan: ver el XML doc de AvanceProyecto.PorcentajeReportado.
            migrationBuilder.AlterColumn<int>(
                name: "PorcentajeReportado",
                table: "ProyectoAvances",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ActividadId",
                table: "ProyectoAvances",
                type: "int",
                nullable: true);

            // ── Actividades ──────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "ProyectoActividades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntregableId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FechaInicioPlan = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFinPlan = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaInicioReal = table.Column<DateOnly>(type: "date", nullable: true),
                    FechaFinReal = table.Column<DateOnly>(type: "date", nullable: true),
                    AvancePct = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResponsableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Responsable = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectoActividades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProyectoActividades_ProyectoEntregables_EntregableId",
                        column: x => x.EntregableId,
                        principalTable: "ProyectoEntregables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoAvances_ActividadId",
                table: "ProyectoAvances",
                column: "ActividadId");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoActividades_EntregableId_Orden",
                table: "ProyectoActividades",
                columns: new[] { "EntregableId", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectoActividades_FechaFinPlan",
                table: "ProyectoActividades",
                column: "FechaFinPlan");

            // Sin cascada ni SetNull: sería la segunda ruta de borrado desde Proyectos hacia
            // ProyectoAvances y SQL Server rechaza el modelo (Msg 1785). El desvínculo lo hace la
            // reconciliación del editor. Ver la configuración de AvanceProyecto.
            migrationBuilder.AddForeignKey(
                name: "FK_ProyectoAvances_ProyectoActividades_ActividadId",
                table: "ProyectoAvances",
                column: "ActividadId",
                principalTable: "ProyectoActividades",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProyectoAvances_ProyectoEntregables_EntregableId",
                table: "ProyectoAvances",
                column: "EntregableId",
                principalTable: "ProyectoEntregables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ── Datos: la auditoría usaba un nombre de evento que ya no existe ───
            // TipoEventoProyecto se guarda como texto; una fila con 'ModificacionHitos' revienta al
            // leerse contra el enum nuevo.
            migrationBuilder.Sql(@"
UPDATE BitacoraProyecto
SET    Tipo = 'ModificacionEstructura'
WHERE  Tipo = 'ModificacionHitos';");

            // ── Datos: el avance del proyecto pasa a calcularse ──────────────────
            // Hasta acá lo declaraba el responsable al reportar. Desde acá es el promedio de sus
            // entregables vigentes, con la regla 0/50/100 del PMI mientras no tengan actividades
            // —que es el estado de todo el portafolio en este momento—. Es un cambio de significado,
            // no una corrección: los porcentajes declarados quedan igual en la bitácora, que es
            // donde consta quién dijo qué y cuándo.
            migrationBuilder.Sql(@"
UPDATE p
SET    p.AvancePct = ISNULL(x.Avance, 0)
FROM   Proyectos p
OUTER APPLY (
    SELECT CAST(ROUND(AVG(CAST(
               CASE e.Estado
                   WHEN 'Completado' THEN 100
                   WHEN 'EnProceso'  THEN 50
                   ELSE 0
               END AS float)), 0) AS int) AS Avance
    FROM   ProyectoEntregables e
    WHERE  e.ProyectoId = p.Id
      AND  e.Estado <> 'Cancelado'
) x;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProyectoAvances_ProyectoActividades_ActividadId",
                table: "ProyectoAvances");

            migrationBuilder.DropForeignKey(
                name: "FK_ProyectoAvances_ProyectoEntregables_EntregableId",
                table: "ProyectoAvances");

            // Las actividades se pierden: el modelo anterior no tiene dónde guardarlas. Las
            // entradas de bitácora imputadas a una sobreviven — pierden la referencia con la
            // columna, no el texto ni el porcentaje.
            migrationBuilder.DropTable(
                name: "ProyectoActividades");

            migrationBuilder.DropIndex(
                name: "IX_ProyectoAvances_ActividadId",
                table: "ProyectoAvances");

            migrationBuilder.DropColumn(
                name: "ActividadId",
                table: "ProyectoAvances");

            // El avance vuelve a ser el snapshot del último reporte, que es lo que significaba
            // antes. Se puede reconstruir porque la bitácora nunca se tocó: para los proyectos sin
            // reportes queda en 0, igual que estaban.
            migrationBuilder.Sql(@"
UPDATE p
SET    p.AvancePct = ISNULL(x.Pct, 0)
FROM   Proyectos p
OUTER APPLY (
    SELECT TOP 1 a.PorcentajeReportado AS Pct
    FROM   ProyectoAvances a
    WHERE  a.ProyectoId = p.Id AND a.PorcentajeReportado IS NOT NULL
    ORDER  BY a.Fecha DESC, a.Id DESC
) x;");

            migrationBuilder.AlterColumn<int>(
                name: "PorcentajeReportado",
                table: "ProyectoAvances",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "EntregableId",
                table: "ProyectoAvances",
                newName: "HitoId");

            migrationBuilder.RenameIndex(
                name: "IX_ProyectoAvances_EntregableId",
                table: "ProyectoAvances",
                newName: "IX_ProyectoAvances_HitoId");

            migrationBuilder.Sql(
                "EXEC sp_rename N'FK_ProyectoEntregables_Proyectos_ProyectoId', " +
                "N'FK_ProyectoHitos_Proyectos_ProyectoId', N'OBJECT';");
            migrationBuilder.Sql(
                "EXEC sp_rename N'PK_ProyectoEntregables', N'PK_ProyectoHitos', N'OBJECT';");

            migrationBuilder.RenameIndex(
                name: "IX_ProyectoEntregables_ProyectoId_Orden",
                table: "ProyectoEntregables",
                newName: "IX_ProyectoHitos_ProyectoId_Orden");

            migrationBuilder.RenameTable(
                name: "ProyectoEntregables",
                newName: "ProyectoHitos");

            migrationBuilder.AddForeignKey(
                name: "FK_ProyectoAvances_ProyectoHitos_HitoId",
                table: "ProyectoAvances",
                column: "HitoId",
                principalTable: "ProyectoHitos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.Sql(@"
UPDATE BitacoraProyecto
SET    Tipo = 'ModificacionHitos'
WHERE  Tipo = 'ModificacionEstructura';");
        }
    }
}
